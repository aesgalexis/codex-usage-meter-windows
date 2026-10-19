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
            if (ReadString(root, "type") != "turn_context" ||
                !TryGetProperty(root, "payload", out var payload)) return null;
            var model = ReadString(payload, "model");
            if (string.IsNullOrWhiteSpace(model)) return null;
            return (model, ReadTimestamp(root, "timestamp") ?? DateTimeOffset.MinValue);
        }
        catch (JsonException) { return null; }
    }

    public static UsageSnapshot? Parse(string jsonLine)
    {
        try
        {
            using var document = JsonDocument.Parse(jsonLine);
            var root = document.RootElement;
            if (!TryGetProperty(root, "payload", out var payload) ||
                !TryGetProperty(payload, "rate_limits", out var rateLimits) ||
                rateLimits.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // A session may also report unrelated product/model limits (for example
            // base_model_inference / gpt-reserve). Those snapshots do not describe
            // Codex's rolling and weekly windows and must never replace them.
            var limitId = ReadString(rateLimits, "limit_id");
            if (!string.IsNullOrWhiteSpace(limitId) &&
                !limitId.Equals("codex", StringComparison.OrdinalIgnoreCase))
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
            var hasCredits = false;

            if (TryGetProperty(rateLimits, "credits", out var credits))
            {
                hasCredits = ReadBool(credits, "has_credits") ?? false;
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
                windows,
                HasCredits: hasCredits);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static UsageWindow? ReadWindow(JsonElement rateLimits, string name, int defaultMinutes)
    {
        if (!TryGetProperty(rateLimits, name, out var window) ||
            !TryGetProperty(window, "used_percent", out var usedElement) ||
            usedElement.ValueKind != JsonValueKind.Number ||
            !usedElement.TryGetDouble(out var usedPercent) || !double.IsFinite(usedPercent)) return null;

        var minutes = ReadInt(window, "window_minutes");
        return new UsageWindow(
            Math.Clamp(usedPercent, 0d, 100d),
            ReadUnixTimestamp(window, "resets_at"),
            minutes is > 0 ? minutes.Value : defaultMinutes);
    }

    private static bool TryGetProperty(JsonElement element, string property, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out value);
    }

    private static string? ReadString(JsonElement element, string property) =>
        TryGetProperty(element, property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, string property) =>
        TryGetProperty(element, property, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

    private static bool? ReadBool(JsonElement element, string property) =>
        TryGetProperty(element, property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static DateTimeOffset? ReadTimestamp(JsonElement element, string property) =>
        TryGetProperty(element, property, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result)
            ? result
            : null;

    private static DateTimeOffset? ReadUnixTimestamp(JsonElement element, string property) =>
        TryGetProperty(element, property, out var value) &&
        value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds) &&
        seconds >= DateTimeOffset.MinValue.ToUnixTimeSeconds() &&
        seconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds()
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
}
