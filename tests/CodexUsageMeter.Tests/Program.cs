using CodexUsageMeter.Infrastructure;
using CodexUsageMeter.Core;
using CodexUsageMeter.App;
using System.Drawing;

const string validEvent = """
{"timestamp":"2026-08-02T10:24:28.684Z","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":33.0,"window_minutes":10080,"resets_at":1786180607},"secondary":null,"credits":{"has_credits":false,"unlimited":false,"balance":"12.50"},"plan_type":"plus"}}}
""";

var snapshot = CodexRateLimitParser.Parse(validEvent);
Assert(snapshot is not null, "El evento válido debe producir un resultado.");
Assert(snapshot!.UsedPercent == 33d, "El porcentaje usado debe conservarse.");
Assert(snapshot.AvailablePercent == 67d, "El porcentaje disponible debe calcularse.");
Assert(snapshot.WindowMinutes == 10080, "La ventana debe leerse.");
Assert(snapshot.PlanType == "plus", "El plan debe leerse.");
Assert(snapshot.CreditBalance == 12.50m, "El saldo debe leerse con formato invariante.");
Assert(snapshot.Windows.Count == 1 && snapshot.Windows[0].WindowMinutes == 10080, "La ventana primary debe conservar su duración.");
Assert(CodexRateLimitParser.Parse("{not-json") is null, "JSON inválido no debe lanzar una excepción.");
Assert(CodexRateLimitParser.Parse("{\"payload\":{}}") is null, "Un evento ajeno debe ignorarse.");
Assert(AppText.CatalogsMatch(), "Los catálogos inglés y español deben contener las mismas claves.");
AppText.SetLanguage(AppText.English);
Assert(AppText.Get("AvailableText", 50) == "50% available", "El catálogo inglés debe formatear el widget.");
AppText.SetLanguage(AppText.Spanish);
Assert(AppText.Get("AvailableText", 50) == "50% disponible", "El catálogo español debe formatear el widget.");

const string twoWindowsEvent = """
{"timestamp":"2026-08-02T11:00:00Z","payload":{"rate_limits":{"primary":{"used_percent":20,"window_minutes":300,"resets_at":1786180607},"secondary":{"used_percent":70,"window_minutes":10080,"resets_at":1786200000},"credits":{"balance":"2"},"plan_type":"plus"}}}
""";
var twoWindows = CodexRateLimitParser.Parse(twoWindowsEvent);
Assert(twoWindows?.Windows.Count == 2, "Primary y secondary deben conservarse simultáneamente.");
Assert(twoWindows?.WindowMinutes == 10080 && twoWindows.AvailablePercent == 30, "La ventana más restrictiva debe ser el indicador efectivo.");

const string secondaryOnlyEvent = """
{"timestamp":"2026-08-02T11:00:00Z","payload":{"rate_limits":{"primary":null,"secondary":{"used_percent":35,"window_minutes":300,"resets_at":1786200000}}}}
""";
var secondaryOnly = CodexRateLimitParser.Parse(secondaryOnlyEvent);
Assert(secondaryOnly?.AvailablePercent == 65 && secondaryOnly.Windows.Count == 1, "Debe aceptarse una ventana secondary sin primary.");
var modelObservation = CodexRateLimitParser.ParseModel("{\"timestamp\":\"2026-08-02T11:30:00Z\",\"type\":\"turn_context\",\"payload\":{\"model\":\"gpt-5.6-sol\"}}");
Assert(modelObservation?.Model == "gpt-5.6-sol", "El modelo activo debe extraerse del turn_context.");

var missingProvider = new CodexSessionUsageProvider(Path.Combine(Path.GetTempPath(), $"codex-usage-meter-missing-{Guid.NewGuid():N}"));
var missingResult = await missingProvider.GetLatestAsync();
Assert(!missingResult.IsSuccess && missingResult.Error is not null, "La ausencia de Codex debe producir un estado en espera, no una excepción.");
Assert(missingResult.FailureKind == UsageFailureKind.SessionsMissing, "La ausencia de sesiones debe distinguirse de un error temporal.");

var testRoot = Path.Combine(Path.GetTempPath(), $"codex-usage-meter-tests-{Guid.NewGuid():N}");
var sessionDirectory = Path.Combine(testRoot, "sessions", "2026", "08", "02");
Directory.CreateDirectory(sessionDirectory);
try
{
    await File.WriteAllTextAsync(Path.Combine(sessionDirectory, "session.jsonl"), $"{{\"type\":\"unrelated\"}}{Environment.NewLine}{validEvent}");
    var providerResult = await new CodexSessionUsageProvider(testRoot).GetLatestAsync();
    Assert(providerResult.IsSuccess, "El proveedor debe encontrar un evento dentro de la jerarquía de sesiones.");
    Assert(providerResult.Snapshot?.AvailablePercent == 67d, "El proveedor debe devolver el snapshot más reciente.");
}
finally
{
    Directory.Delete(testRoot, recursive: true);
}

var orderingRoot = Path.Combine(Path.GetTempPath(), $"codex-usage-meter-ordering-{Guid.NewGuid():N}");
var orderingSessions = Path.Combine(orderingRoot, "sessions");
Directory.CreateDirectory(orderingSessions);
try
{
    var olderSnapshot = twoWindowsEvent.Replace("2026-08-02T11:00:00Z", "2026-08-02T09:00:00Z");
    var newestSnapshot = twoWindowsEvent.Replace("2026-08-02T11:00:00Z", "2026-08-02T12:00:00Z");
    var recentlyModified = Path.Combine(orderingSessions, "recently-modified.jsonl");
    var olderModified = Path.Combine(orderingSessions, "older-modified.jsonl");
    await File.WriteAllTextAsync(recentlyModified, olderSnapshot);
    var incompleteTailPrefix = new string('x', 1024 * 1024 + 100);
    var modelEvent = "{\"timestamp\":\"2026-08-02T11:30:00Z\",\"type\":\"turn_context\",\"payload\":{\"model\":\"gpt-5.6-terra\"}}";
    await File.WriteAllTextAsync(olderModified, $"{incompleteTailPrefix}{Environment.NewLine}{modelEvent}{Environment.NewLine}{newestSnapshot}{Environment.NewLine}{olderSnapshot}");
    File.SetLastWriteTimeUtc(recentlyModified, DateTime.UtcNow);
    File.SetLastWriteTimeUtc(olderModified, DateTime.UtcNow.AddMinutes(-1));
    var orderedResult = await new CodexSessionUsageProvider(orderingRoot).GetLatestAsync();
    Assert(orderedResult.Snapshot?.ObservedAt == DateTimeOffset.Parse("2026-08-02T12:00:00Z"), "Debe elegirse el timestamp más reciente, no el archivo modificado más recientemente ni su última línea.");
    Assert(orderedResult.Snapshot?.ActiveModel == "gpt-5.6-terra", "El snapshot debe conservar el modelo más reciente de su sesión.");
}
finally
{
    Directory.Delete(orderingRoot, recursive: true);
}

var observedAt = DateTimeOffset.Parse("2026-08-02T10:00:00Z");
var first = new UsageSnapshot(49, observedAt.AddHours(1), 60, "plus", 0, observedAt);
var crossed = first with { UsedPercent = 76, ObservedAt = observedAt.AddMinutes(1) };
var defaultNotifications = NotificationOptions.Default;
Assert(!defaultNotifications.NotifyOnPercentChange &&
       !defaultNotifications.NotifyAt50Percent &&
       !defaultNotifications.NotifyAt75Percent &&
       !defaultNotifications.NotifyAt90Percent,
    "Las notificaciones de porcentaje deben estar desactivadas por defecto.");
var newSettings = new AppSettings();
Assert(!newSettings.NotifyOnPercentChange &&
       !newSettings.NotifyAt50Percent &&
       !newSettings.NotifyAt75Percent &&
       !newSettings.NotifyAt90Percent,
    "Una instalación nueva no debe activar notificaciones de porcentaje.");
Assert(newSettings.UsageBarThickness == 3, "La barra de uso debe usar 3 px por defecto.");
Assert(newSettings.UsageBarDisplay == "auto", "La barra de uso debe elegir pantalla automáticamente por defecto.");
var thresholdOptions = NotificationOptions.Default with
{
    NotifyAt50Percent = true,
    NotifyAt75Percent = true,
    NotifyAt90Percent = true
};
var thresholdNotice = UsageNotificationEvaluator.Evaluate(first, crossed, thresholdOptions);
Assert(thresholdNotice == new UsageNotification(UsageNotificationKind.ThresholdReached, 75), "Un salto debe avisar solo del umbral más alto cruzado.");
Assert(UsageNotificationEvaluator.Evaluate(null, first, NotificationOptions.Default) is null, "El arranque no debe generar notificaciones.");

var percentOptions = NotificationOptions.Default with
{
    NotifyOnPercentChange = true,
    NotifyAt50Percent = false,
    NotifyAt75Percent = false,
    NotifyAt90Percent = false
};
var percentNotice = UsageNotificationEvaluator.Evaluate(first, first with { UsedPercent = 50.2 }, percentOptions);
Assert(percentNotice?.Kind == UsageNotificationKind.PercentChanged, "El cambio de porcentaje entero debe poder notificarse.");

var reset = crossed with
{
    UsedPercent = 3,
    ResetsAt = observedAt.AddHours(2),
    ObservedAt = observedAt.AddHours(1)
};
var resetNotice = UsageNotificationEvaluator.Evaluate(crossed, reset, NotificationOptions.Default);
Assert(resetNotice?.Kind == UsageNotificationKind.LimitReset, "El avance del reinicio con menor consumo debe detectarse.");

var temporaryFailure = UsageResult.Failure("temporary", UsageFailureKind.ReadError);
var retainedState = UsageStatePolicy.Resolve(first, temporaryFailure, observedAt.AddMinutes(2), TimeSpan.FromMinutes(30));
Assert(retainedState.Snapshot == first && retainedState.IsStale, "Un error temporal debe conservar el último snapshot válido y marcarlo como desactualizado.");
var replayedState = UsageStatePolicy.Resolve(crossed, UsageResult.Success(first), observedAt.AddMinutes(2), TimeSpan.FromMinutes(30));
Assert(replayedState.Snapshot == crossed && replayedState.IsStale, "Un snapshot reproducido con timestamp antiguo no debe reemplazar el dato actual.");
var agedState = UsageStatePolicy.Resolve(null, UsageResult.Success(first), observedAt.AddHours(1), TimeSpan.FromMinutes(30));
Assert(agedState.Snapshot == first && agedState.IsStale, "Un snapshot válido pero antiguo debe indicar claramente su antigüedad.");

using (var trayIcon = new StableNotifyIcon())
{
    trayIcon.Text = "Codex Usage Meter smoke test";
    trayIcon.Icon = SystemIcons.Application;
    trayIcon.Visible = true;
    trayIcon.Visible = false;
}

Console.WriteLine("Todas las pruebas han pasado.");
return;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
