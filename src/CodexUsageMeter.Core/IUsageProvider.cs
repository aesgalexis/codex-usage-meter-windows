namespace CodexUsageMeter.Core;

public interface IUsageProvider
{
    Task<UsageResult> GetLatestAsync(CancellationToken cancellationToken = default);
}
