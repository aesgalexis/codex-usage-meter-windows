namespace CodexUsageMeter.Core;

public enum UsageFailureKind { None, SessionsMissing, NoSnapshots, ReadError, AccessDenied }

public sealed record UsageResult(UsageSnapshot? Snapshot, string? Error, UsageFailureKind FailureKind = UsageFailureKind.None)
{
    public bool IsSuccess => Snapshot is not null;

    public static UsageResult Success(UsageSnapshot snapshot) => new(snapshot, null);
    public static UsageResult Failure(string error, UsageFailureKind kind = UsageFailureKind.ReadError) => new(null, error, kind);
}
