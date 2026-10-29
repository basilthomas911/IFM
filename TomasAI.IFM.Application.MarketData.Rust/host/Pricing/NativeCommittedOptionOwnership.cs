using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;
using TomasAI.IFM.Application.MarketData.Rust.Api;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Realizes PostgreSQL-committed ownership directly in native scopes.
/// Application intent and revision checks remain unchanged; no worker supervisor,
/// provider process or uncommitted caller-supplied ownership is used.</summary>
public sealed class NativeCommittedOptionOwnership(NativeMarketDataEpochFactory epochs,NativeHostState state,
    NativeSessionPublications publications,DatasetDesiredSubscriptionRegistry desired,IDurableSubscriptionIntentStore store,
    ICompositionMarketDataApi market,TimeProvider clock):ICommittedOptionOwnership
{
    /// <inheritdoc/>
    public async Task<DatasetWorkerProcessSnapshot> ApplyDesiredManifestAsync(string dataset,CancellationToken token)
    {
        var epoch=epochs.Current;
        if(dataset!="GLBX.MDP3"||!desired.TryGet(dataset,epoch.ValueDate,out var manifest))throw new InvalidOperationException("Desired native option manifest is unavailable.");
        await epoch.PrepareOptionUnderlyingsAsync(manifest.Contracts,TimeSpan.FromMinutes(2),token).ConfigureAwait(false);
        publications.Current?.SetOptionManifestRevision(manifest.Revision);
        state.OptionManifestApplied(manifest.Revision);
        var current=state.Capture(true).Datasets.Single(x=>x.Role=="Options"&&x.Dataset==dataset);
        return new(){Dataset=dataset,WorkerInstanceId=current.WorkerInstanceId,GenerationId=current.GenerationId,ProcessId=Environment.ProcessId,
            StartedOnUtc=clock.GetUtcNow().UtcDateTime,Running=true,Healthy=current.Healthy,GracefulStopSucceeded=false,ForcedTermination=false,
            Detail="Native Tokio option manifest qualified; no child process.",ManifestRevision=manifest.Revision,ManifestFingerprint=manifest.Fingerprint,ControlResponsive=true,DataPlaneHealthy=current.Healthy};
    }
    /// <inheritdoc/>
    public async Task<WorkerOptionChainResult> ApplyBusinessOwnershipAsync(string scope,string dataset,WorkerOptionChainRequest chain,CancellationToken token)
    {
        var epoch=epochs.Current;
        if(dataset!="GLBX.MDP3"||epoch.ValueDate!=chain.ValueDate||epoch.OptionGeneration!=chain.GenerationId)throw new InvalidOperationException("Committed native option owner is stale.");
        var committed=await store.ReadAsync(scope,dataset,token).ConfigureAwait(false);
        return await market.ReleaseAsync(dataset,CommittedOptionChainOwnership.Create(committed,chain),token).ConfigureAwait(false);
    }
}
