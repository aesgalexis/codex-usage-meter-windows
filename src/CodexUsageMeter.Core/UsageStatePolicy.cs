namespace CodexUsageMeter.Core;

public sealed record UsageState(UsageSnapshot? Snapshot, bool IsStale, UsageFailureKind FailureKind);

public static class UsageStatePolicy
{
    public static UsageState Resolve(
        UsageSnapshot? current,
        UsageResult result,
        DateTimeOffset now,
        TimeSpan staleAfter)
    {
        if (result.Snapshot is { } candidate)
        {
            if (current is not null && candidate.ObservedAt < current.ObservedAt)
                return new UsageState(current, true, UsageFailureKind.NoSnapshots);

            return new UsageState(candidate, now - candidate.ObservedAt > staleAfter, UsageFailureKind.None);
        }

        return new UsageState(current, current is not null, result.FailureKind);
    }
}
