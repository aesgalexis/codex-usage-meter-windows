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
                !payload.TryGetProperty("rate_limits", out var rateLimits) ||
                !rateLimits.TryGetProperty("primary", out var primary) ||
                primary.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                !primary.TryGetProperty("used_percent", out var usedElement) ||
                !usedElement.TryGetDouble(out var usedPercent))
            {
                return null;
            }

            var observedAt = ReadTimestamp(root, "timestamp") ?? DateTimeOffset.UtcNow;
            var resetsAt = ReadUnixTimestamp(primary, "resets_at");
            var windowMinutes = ReadInt(primary, "window_minutes");
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
                Math.Clamp(usedPercent, 0d, 100d),
                resetsAt,
                windowMinutes,
                planType,
                balance,
                observedAt);
        }
        catch (JsonException)
        {
            return null;
        }
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
