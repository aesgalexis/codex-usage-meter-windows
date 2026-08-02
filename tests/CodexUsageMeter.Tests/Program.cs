using CodexUsageMeter.Infrastructure;
using CodexUsageMeter.Core;

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
Assert(CodexRateLimitParser.Parse("{not-json") is null, "JSON inválido no debe lanzar una excepción.");
Assert(CodexRateLimitParser.Parse("{\"payload\":{}}") is null, "Un evento ajeno debe ignorarse.");

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

var observedAt = DateTimeOffset.Parse("2026-08-02T10:00:00Z");
var first = new UsageSnapshot(49, observedAt.AddHours(1), 60, "plus", 0, observedAt);
var crossed = first with { UsedPercent = 76, ObservedAt = observedAt.AddMinutes(1) };
var thresholdNotice = UsageNotificationEvaluator.Evaluate(first, crossed, NotificationOptions.Default);
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

Console.WriteLine("Todas las pruebas han pasado.");
return;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
