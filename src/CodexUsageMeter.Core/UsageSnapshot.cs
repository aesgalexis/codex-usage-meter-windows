namespace CodexUsageMeter.Core;

public sealed record UsageSnapshot(
    double UsedPercent,
    DateTimeOffset? ResetsAt,
    int? WindowMinutes,
    string? PlanType,
    decimal? CreditBalance,
    DateTimeOffset ObservedAt)
{
    public double AvailablePercent => Math.Clamp(100d - UsedPercent, 0d, 100d);
}
