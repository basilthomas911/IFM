using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Derives financial feed ownership from PostgreSQL and realizes it only in the option worker.</summary>
public sealed class HostCommittedOptionOwnership(MarketDataWorkerHost host, DatasetDesiredSubscriptionRegistry desired,
    IDurableSubscriptionIntentStore store) : ICommittedOptionOwnership
{
    public Task<DatasetWorkerProcessSnapshot> ApplyDesiredManifestAsync(string dataset, CancellationToken token)
    {
        var current = host.GetBackendStatus().Datasets.Single(state => state.Role == "Options" && state.Dataset == dataset && state.Lifecycle == "Healthy");
        if (!desired.TryGet(dataset, current.ValueDate!.Value, out var manifest)) throw new InvalidOperationException("Desired option manifest is unavailable.");
        return host.ApplyOptionsManifestAsync(current.WorkerInstanceId, manifest, token);
    }
    public async Task<WorkerOptionChainResult> ApplyBusinessOwnershipAsync(string scope, string dataset,
        WorkerOptionChainRequest chain, CancellationToken token)
    {
        var current = host.GetBackendStatus().Datasets.Single(state => state.Role == "Options" && state.Dataset == dataset
            && state.GenerationId == chain.GenerationId && state.Lifecycle == "Healthy");
        var committed = await store.ReadAsync(scope, dataset, token).ConfigureAwait(false);
        return await host.ApplyCommittedOwnershipAsync("Options", current.WorkerInstanceId, chain,
            CommittedOptionChainOwnership.Create(committed, chain), token).ConfigureAwait(false);
    }
}
