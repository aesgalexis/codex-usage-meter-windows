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
    public UsageWindow? FiveHourWindow =>
        Windows.FirstOrDefault(window => window.WindowMinutes == 5 * 60) ??
        Windows.Where(window => window.WindowMinutes is >= 240 and <= 360)
            .OrderBy(window => Math.Abs(window.WindowMinutes!.Value - 5 * 60))
            .FirstOrDefault();
    public UsageWindow WeeklyWindow =>
        Windows.FirstOrDefault(window => window.WindowMinutes == 7 * 24 * 60) ??
        Windows.OrderByDescending(window => window.WindowMinutes ?? 0).First();
}
