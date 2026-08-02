namespace CodexUsageMeter.Core;

public sealed record UsageWindow(double UsedPercent, DateTimeOffset? ResetsAt, int? WindowMinutes)
{
    public double AvailablePercent => Math.Clamp(100d - UsedPercent, 0d, 100d);
}

public sealed record UsageSnapshot(
    double UsedPercent,
    DateTimeOffset? ResetsAt,
    int? WindowMinutes,
    string? PlanType,
    decimal? CreditBalance,
    DateTimeOffset ObservedAt,
    IReadOnlyList<UsageWindow>? RateLimitWindows = null,
    string? ActiveModel = null)
{
    public double AvailablePercent => Math.Clamp(100d - UsedPercent, 0d, 100d);
    public IReadOnlyList<UsageWindow> Windows => RateLimitWindows ?? [new UsageWindow(UsedPercent, ResetsAt, WindowMinutes)];
}
