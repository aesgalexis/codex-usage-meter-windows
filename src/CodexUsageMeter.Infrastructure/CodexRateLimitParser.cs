using System.Globalization;
using System.Text.Json;
using CodexUsageMeter.Core;

namespace CodexUsageMeter.Infrastructure;

public static class CodexRateLimitParser
{
    public static (string Model, DateTimeOffset ObservedAt)? ParseModel(string jsonLine)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonLine);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "turn_context" ||
                !root.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(model.GetString())) return null;
            return (model.GetString()!, ReadTimestamp(root, "timestamp") ?? DateTimeOffset.MinValue);
        }
        catch (JsonException) { return null; }
    }

    public static UsageSnapshot? Parse(string jsonLine)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonLine);
            var root = document.RootElement;
            if (!root.TryGetProperty("payload", out var payload) ||
                !payload.TryGetProperty("rate_limits", out var rateLimits))
            {
                return null;
            }

            var observedAt = ReadTimestamp(root, "timestamp") ?? DateTimeOffset.UtcNow;
            // Codex identifies the rolling window as primary and the weekly window as
            // secondary. Some client versions omit window_minutes, so preserve that
            // semantic identity instead of accidentally treating the first window as weekly.
            var windows = new[] { (Name: "primary", DefaultMinutes: 5 * 60), (Name: "secondary", DefaultMinutes: 7 * 24 * 60) }
                .Select(item => ReadWindow(rateLimits, item.Name, item.DefaultMinutes))
                .Where(window => window is not null)
                .Cast<UsageWindow>()
                .OrderBy(window => window.WindowMinutes ?? int.MaxValue)
                .ToArray();
            if (windows.Length == 0) return null;

            var effective = windows.OrderBy(window => window.AvailablePercent).First();
            var planType = ReadString(rateLimits, "plan_type");
            decimal? balance = null;

            if (rateLimits.TryGetProperty("credits", out var credits))
            {
                var balanceText = ReadString(credits, "balance");
                if (decimal.TryParse(balanceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    balance = parsed;
                }
            }

            return new UsageSnapshot(
                effective.UsedPercent,
                effective.ResetsAt,
                effective.WindowMinutes,
                planType,
                balance,
                observedAt,
                windows);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UsageWindow? ReadWindow(JsonElement rateLimits, string name, int defaultMinutes)
    {
        if (!rateLimits.TryGetProperty(name, out var window) ||
            window.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !window.TryGetProperty("used_percent", out var usedElement) ||
            !usedElement.TryGetDouble(out var usedPercent)) return null;

        return new UsageWindow(
            Math.Clamp(usedPercent, 0d, 100d),
            ReadUnixTimestamp(window, "resets_at"),
            ReadInt(window, "window_minutes") ?? defaultMinutes);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;

    private static DateTimeOffset? ReadTimestamp(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;

    private static DateTimeOffset? ReadUnixTimestamp(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
