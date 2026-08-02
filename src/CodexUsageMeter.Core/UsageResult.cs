namespace CodexUsageMeter.Core;

public sealed record UsageResult(UsageSnapshot? Snapshot, string? Error)
{
    public bool IsSuccess => Snapshot is not null;

    public static UsageResult Success(UsageSnapshot snapshot) => new(snapshot, null);
    public static UsageResult Failure(string error) => new(null, error);
}
