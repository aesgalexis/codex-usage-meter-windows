namespace CodexUsageMeter.Core;

public sealed record NotificationOptions(
    bool NotifyOnPercentChange,
    bool NotifyAt50Percent,
    bool NotifyAt75Percent,
    bool NotifyAt90Percent,
    bool NotifyOnReset)
{
    public static NotificationOptions Default { get; } = new(
        NotifyOnPercentChange: false,
        NotifyAt50Percent: false,
        NotifyAt75Percent: false,
        NotifyAt90Percent: false,
        NotifyOnReset: true);
}
