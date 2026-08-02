using System.Globalization;
using System.Text.Json;
using CodexUsageMeter.Core;

namespace CodexUsageMeter.Infrastructure;

public static class CodexRateLimitParser
{
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
            var windows = new[] { "primary", "secondary" }
                .Select(name => ReadWindow(rateLimits, name))
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

    private static UsageWindow? ReadWindow(JsonElement rateLimits, string name)
    {
        if (!rateLimits.TryGetProperty(name, out var window) ||
            window.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
            !window.TryGetProperty("used_percent", out var usedElement) ||
            !usedElement.TryGetDouble(out var usedPercent)) return null;

        return new UsageWindow(
            Math.Clamp(usedPercent, 0d, 100d),
            ReadUnixTimestamp(window, "resets_at"),
            ReadInt(window, "window_minutes"));
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
