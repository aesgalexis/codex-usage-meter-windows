namespace CodexUsageMeter.Core;

public interface IResetCreditProvider
{
    Task<int?> GetAvailableAsync(CancellationToken cancellationToken = default);
}
