using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Subscriptions;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
public sealed partial class NativeOwnerBusinessOperations
{
    /// <summary>Commands native committed ownership. No C# realization registry,
    /// polling, lease renewal or secondary recovery timeout is involved.</summary>
    public Task<DurableRealization?> ReconcileCommittedSubscriptionsAsync(CancellationToken token=default)
        =>backend.Runtime.ReconcileDatasetOwnerCommittedAsync(token);
    /// <summary>Releases copied logical demand through native ownership checks.
    /// Physical cancellation/join and remaining shared demand belong to Rust.</summary>
    public Task<WorkerOptionChainResult> ReleaseCompositionAsync(string dataset,WorkerOptionChainRelease request,CancellationToken token=default)
        =>backend.Runtime.ReleaseDatasetOwnerCompositionAsync(dataset,request,token);
}
