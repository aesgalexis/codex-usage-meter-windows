namespace CodexUsageMeter.Core;

public static class UsageNotificationEvaluator
{
    public static UsageNotification? Evaluate(
        UsageSnapshot? previous,
        UsageSnapshot current,
        NotificationOptions options)
    {
        if (previous is null)
        {
            return null;
        }

        var previousWindow = previous.WeeklyWindow;
        var currentWindow = current.WeeklyWindow;
        if (previousWindow.WindowMinutes != currentWindow.WindowMinutes)
        {
            return null;
        }

        if (options.NotifyOnReset && HasReset(previousWindow, currentWindow))
        {
            return new UsageNotification(UsageNotificationKind.LimitReset);
        }

        var threshold = HighestCrossedThreshold(previousWindow.UsedPercent, currentWindow.UsedPercent, options);
        if (threshold is not null)
        {
            return new UsageNotification(UsageNotificationKind.ThresholdReached, threshold);
        }

        if (options.NotifyOnPercentChange &&
            (int)Math.Floor(previousWindow.UsedPercent) != (int)Math.Floor(currentWindow.UsedPercent))
        {
            return new UsageNotification(UsageNotificationKind.PercentChanged);
        }

        return null;
    }

    private static bool HasReset(UsageWindow previous, UsageWindow current) =>
        previous.ResetsAt is { } previousReset &&
        current.ResetsAt is { } currentReset &&
        currentReset > previousReset &&
        current.UsedPercent < previous.UsedPercent;

    private static int? HighestCrossedThreshold(
        double previousUsed,
        double currentUsed,
        NotificationOptions options)
    {
        if (currentUsed <= previousUsed)
        {
            return null;
        }

        var thresholds = new[]
        {
            (Value: 50, Enabled: options.NotifyAt50Percent),
            (Value: 75, Enabled: options.NotifyAt75Percent),
            (Value: 90, Enabled: options.NotifyAt90Percent)
        };

        return thresholds
            .Where(item => item.Enabled && previousUsed < item.Value && currentUsed >= item.Value)
            .Select(item => (int?)item.Value)
            .LastOrDefault();
    }
}
