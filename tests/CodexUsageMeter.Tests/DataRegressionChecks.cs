using System.Globalization;
using System.IO;
using CodexUsageMeter.App;
using CodexUsageMeter.Core;
using CodexUsageMeter.Infrastructure;

internal static class DataRegressionChecks
{
    public static async Task RunAsync()
    {
        CheckMissingWeeklyWindow();
        var directory = Path.Combine(Path.GetTempPath(), "CodexUsageMeter-data-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            await CheckActivityTimeoutAsync(directory);
            await CheckCodexHomeAsync(directory);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckMissingWeeklyWindow()
    {
        var previous = ParsePrimaryOnly(49, "2026-11-05T10:00:00Z");
        var current = ParsePrimaryOnly(51, "2026-11-05T10:01:00Z");
        Require(current.FiveHourWindow?.UsedPercent == 51 && current.WeeklyWindow is null,
            "La ventana de cinco horas debe conservarse sin presentarla como semanal.");

        var options = new NotificationOptions(true, true, true, true, true);
        Require(UsageNotificationEvaluator.Evaluate(previous, current, options) is null,
            "Un cambio en primary sin ventana semanal no debe generar avisos semanales.");

        var weekly = current with
        {
            RateLimitWindows = [new UsageWindow(80, DateTimeOffset.Parse("2026-11-12T10:00:00Z"), 10080)]
        };
        Require(UsageNotificationEvaluator.Evaluate(previous, weekly, options) is null &&
                UsageNotificationEvaluator.Evaluate(weekly, current, options) is null,
            "La aparición o desaparición de la ventana semanal no debe confundirse con cambios de consumo.");

        var reset = current with
        {
            RateLimitWindows = [new UsageWindow(1, current.ResetsAt?.AddHours(5), 300)]
        };
        Require(UsageNotificationEvaluator.Evaluate(current, reset, options) is null,
            "El reinicio de primary sin ventana semanal no debe anunciarse como reinicio semanal.");
    }

    private static UsageSnapshot ParsePrimaryOnly(int usedPercent, string timestamp)
    {
        const string json = """
            {"timestamp":"TIMESTAMP","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":PERCENT,"window_minutes":300,"resets_at":1793876400},"secondary":null}}}
            """;
        return CodexRateLimitParser.Parse(json.Replace("TIMESTAMP", timestamp)
                   .Replace("PERCENT", usedPercent.ToString(CultureInfo.InvariantCulture)))
               ?? throw new InvalidOperationException("El snapshot sintético de primary debe ser válido.");
    }

    private static async Task CheckActivityTimeoutAsync(string directory)
    {
        var path = Path.Combine(directory, "activity.jsonl");
        await File.WriteAllTextAsync(path, "{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\"}}\n");
        Require(await TaskActivityReader.ReadLatestAsync(path, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10)) == true,
            "El inicio reciente debe activar el estado de actividad.");

        await File.AppendAllTextAsync(path,
            "{\"type\":\"event_msg\",\"payload\":{\"type\":\"item_completed\",\"padding\":\"" +
            new string('x', 1_100_000) + "\"}}\n");
        var now = DateTimeOffset.UtcNow;
        Require(await TaskActivityReader.ReadLatestAsync(path, now, TimeSpan.FromMinutes(10)) is null,
            "Una cola reciente sin eventos de estado debe conservar la actividad conocida del llamador.");
        File.SetLastWriteTimeUtc(path, now.UtcDateTime.AddMinutes(-11));
        Require(await TaskActivityReader.ReadLatestAsync(path, now, TimeSpan.FromMinutes(10)) == false,
            "La inactividad debe apagar el brillo aunque task_started haya salido del último MiB.");
    }

    private static async Task CheckCodexHomeAsync(string directory)
    {
        var originalHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var environmentHome = Path.Combine(directory, "environment-home");
        var explicitHome = Path.Combine(directory, "explicit-home");
        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", environmentHome);
            Require(CodexHomeResolver.Resolve() == environmentHome &&
                    CodexHomeResolver.Resolve(explicitHome) == explicitHome,
                "La ruta explícita debe prevalecer sobre CODEX_HOME y este sobre la ruta predeterminada.");

            await WriteSessionAsync(environmentHome, 20);
            await WriteSessionAsync(explicitHome, 70);
            Require((await new CodexSessionUsageProvider().GetLatestAsync()).Snapshot?.WeeklyWindow?.UsedPercent == 20,
                "El proveedor predeterminado debe leer las sesiones sintéticas de CODEX_HOME.");
            Require((await new CodexSessionUsageProvider(explicitHome).GetLatestAsync()).Snapshot?.WeeklyWindow?.UsedPercent == 70,
                "La ruta explícita del proveedor debe prevalecer sobre CODEX_HOME.");

            Environment.SetEnvironmentVariable("CODEX_HOME", null);
            var defaultHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
            Require(CodexHomeResolver.Resolve() == defaultHome,
                "Sin CODEX_HOME debe conservarse la ubicación predeterminada sin leer sesiones reales.");
            Environment.SetEnvironmentVariable("CODEX_HOME", " ");
            Require(CodexHomeResolver.Resolve() == defaultHome,
                "Un CODEX_HOME vacío debe recuperar la ubicación predeterminada.");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", originalHome);
        }
    }

    private static async Task WriteSessionAsync(string home, int usedPercent)
    {
        var sessions = Path.Combine(home, "sessions");
        Directory.CreateDirectory(sessions);
        const string json = """
            {"timestamp":"2026-11-05T10:00:00Z","payload":{"rate_limits":{"primary":null,"secondary":{"used_percent":PERCENT,"window_minutes":10080}}}}
            """;
        await File.WriteAllTextAsync(Path.Combine(sessions, "synthetic.jsonl"),
            json.Replace("PERCENT", usedPercent.ToString(CultureInfo.InvariantCulture)));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
