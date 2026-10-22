using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;

/// <summary>Backend-only realization of ownership already committed in the business journal.</summary>
public interface ICommittedOptionOwnership
{
    Task<DatasetWorkerProcessSnapshot> ApplyDesiredManifestAsync(string dataset, CancellationToken cancellationToken);
    Task<WorkerOptionChainResult> ApplyBusinessOwnershipAsync(string authorityScope, string dataset, WorkerOptionChainRequest chain, CancellationToken cancellationToken);
}
