namespace CodexUsageMeter.Core;

public enum UsageNotificationKind
{
    PercentChanged,
    ThresholdReached,
    LimitReset
}

public sealed record UsageNotification(UsageNotificationKind Kind, int? Threshold = null);
