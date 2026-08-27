using CodexUsageMeter.Core;

namespace CodexUsageMeter.App;

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string? Language { get; set; }
    public bool NotifyOnPercentChange { get; set; }
    public bool NotifyAt50Percent { get; set; }
    public bool NotifyAt75Percent { get; set; }
    public bool NotifyAt90Percent { get; set; }
    public bool NotifyOnReset { get; set; }
    public bool WidgetPinned { get; set; }
    public bool WidgetEnabled { get; set; }
    public bool WidgetCompact { get; set; }
    public bool UsageBarEnabled { get; set; } = true;
    public int UsageBarThickness { get; set; } = 3;
    public string UsageBarDisplay { get; set; } = "auto";
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }

    public NotificationOptions ToNotificationOptions() => new(
        NotifyOnPercentChange,
        NotifyAt50Percent,
        NotifyAt75Percent,
        NotifyAt90Percent,
        NotifyOnReset);
}
