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
        NotifyAt50Percent: true,
        NotifyAt75Percent: true,
        NotifyAt90Percent: true,
        NotifyOnReset: true);
}
