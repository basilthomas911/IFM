using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
public sealed partial class NativeOwnerBusinessOperations
{
    /// <summary>Submits copied normalized candidates. Rust owns exact-version
    /// lookup, qualification, context preparation and complete source recovery.</summary>
    public Task<CompositionDiscoveryResult> AcquireOptionScopeAsync(CompositionDiscoveryRequest request,CancellationToken token=default)
        =>AcquireOptionScopeCoreAsync(request,token,null);
    async Task<CompositionDiscoveryResult> AcquireOptionScopeCoreAsync(CompositionDiscoveryRequest request,CancellationToken token,NativePreparationOrigin? origin)
    {
        ArgumentNullException.ThrowIfNull(request);token.ThrowIfCancellationRequested();RequireDate(request.ValueDate);
        backend.Runtime.ConfigureDatasetOwnerDiscovery(request,backend.Scope,backend.OptionGateway,backend.Broker,backend.Policy,origin);
        // A known empty/rejected business scope has no physical source. The
        // native result distinguishes it from a ready active chain.
        await backend.Runtime.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false,token).ConfigureAwait(false);
        var result=backend.Runtime.ReadDatasetOwnerDiscovery(request.LeaseId);
        if(result.Lease is null)return result;
        // Accepted desired ownership belongs to Rust. A delayed/uncertain
        // business-store outcome must not let this observer withdraw a newer
        // activation of the same owner. Release remains an explicit command.
        return await Get<NativeCompositionPlanPersistence>().BindAsync(request,result.Lease,token).ConfigureAwait(false);
    }
    /// <summary>Releases stable business demand, never a C# provider token.</summary>
    public Task<WorkerOptionChainResult> ReleaseOptionScopeAsync(WorkerOptionChainRequest request,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return backend.Runtime.ReleaseDatasetOwnerCompositionAsync("GLBX.MDP3",new(request.ScopeId,request.LeaseId,request.GenerationId),token);
    }
}
