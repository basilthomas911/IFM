using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed class UnavailableDatabentoDatasetProcessRecovery : IDatabentoDatasetProcessRecovery
{
    public Task<DatabentoDatasetResetResult> ReplaceProcessAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken) 
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new DatabentoDatasetResetResult(
            request.Dataset,
            request.ExpectedGenerationId,
            Guid.Empty,
            false,
            "No supervised dataset-process recovery implementation is registered."));
    }
}
