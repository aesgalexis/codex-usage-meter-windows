using CodexUsageMeter.Core;

namespace CodexUsageMeter.App;

public sealed class AppSettings
{
    public bool NotifyOnPercentChange { get; set; }
    public bool NotifyAt50Percent { get; set; } = true;
    public bool NotifyAt75Percent { get; set; } = true;
    public bool NotifyAt90Percent { get; set; } = true;
    public bool NotifyOnReset { get; set; } = true;
    public bool WidgetPinned { get; set; }
    public bool WidgetCompact { get; set; }
    public double? WidgetLeft { get; set; }
    public double? WidgetTop { get; set; }

    public NotificationOptions ToNotificationOptions() => new(
        NotifyOnPercentChange,
        NotifyAt50Percent,
        NotifyAt75Percent,
        NotifyAt90Percent,
        NotifyOnReset);
}
