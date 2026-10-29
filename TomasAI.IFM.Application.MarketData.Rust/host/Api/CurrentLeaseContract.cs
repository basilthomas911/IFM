using TomasAI.IFM.Application.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Preserves the current backend's additive Stage 4 API behavior.
/// Those owner-aware overloads are explicitly disabled in the current C# backend;
/// committed business intent uses native journal ownership in this facade. This
/// adapter never converts them into ownerless boolean routes or claims readiness.</summary>
public sealed class CurrentLeaseContract
{
    /// <summary>Exact current result for additive futures lease requests.</summary>
    public Task<SubscriptionLeaseResult> StartStreamingFuturesTickDataAsync(SubscriptionAcquireRequest request,CancellationToken token=default){ArgumentNullException.ThrowIfNull(request);return Task.FromResult(SubscriptionLeaseResult.Disabled(request.OperationId));}
    /// <summary>Exact current result for additive individual-option lease requests.</summary>
    public Task<SubscriptionLeaseResult> StartStreamingFuturesOptionTickDataAsync(SubscriptionAcquireRequest request,CancellationToken token=default){ArgumentNullException.ThrowIfNull(request);return Task.FromResult(SubscriptionLeaseResult.Disabled(request.OperationId));}
    /// <summary>Exact current result for additive chain lease requests.</summary>
    public Task<SubscriptionLeaseResult> StartStreamingFuturesOptionChainDataAsync(SubscriptionAcquireRequest request,CancellationToken token=default){ArgumentNullException.ThrowIfNull(request);return Task.FromResult(SubscriptionLeaseResult.Disabled(request.OperationId));}
    /// <summary>Exact current result for additive batch lease requests.</summary>
    public Task<SubscriptionLeaseResult> AcquireSelectedSubscriptionLeasesAsync(SubscriptionAcquireBatchRequest request,CancellationToken token=default){ArgumentNullException.ThrowIfNull(request);return Task.FromResult(SubscriptionLeaseResult.Disabled(request.OperationId));}
    /// <summary>Exact current result for additive lease renewals.</summary>
    public Task<SubscriptionLeaseResult> RenewSubscriptionLeaseAsync(SubscriptionRenewRequest request,CancellationToken token=default){ArgumentNullException.ThrowIfNull(request);return Task.FromResult(SubscriptionLeaseResult.Disabled(request.OperationId));}
    /// <summary>Exact current result for additive lease releases.</summary>
    public Task<SubscriptionLeaseResult> ReleaseSubscriptionLeaseAsync(SubscriptionReleaseRequest request,CancellationToken token=default){ArgumentNullException.ThrowIfNull(request);return Task.FromResult(SubscriptionLeaseResult.Disabled(request.OperationId));}
    /// <summary>Exact current result for additive lease observation.</summary>
    public Task<SubscriptionLeaseResult> GetSubscriptionLeasesAsync(SubscriptionOwnerQuery request,CancellationToken token=default)=>Task.FromResult(SubscriptionLeaseResult.Disabled(Guid.Empty));
}
