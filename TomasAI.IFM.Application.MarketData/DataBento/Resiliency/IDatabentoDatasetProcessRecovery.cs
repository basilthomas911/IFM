using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface IDatabentoDatasetProcessRecovery
{
    // Read-only exact-generation evidence; absence is not proof of process exit.
    bool HasExited(string dataset, Guid expectedGeneration) => false;

    Task<DatabentoDatasetResetResult> ReplaceProcessAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken);
}
