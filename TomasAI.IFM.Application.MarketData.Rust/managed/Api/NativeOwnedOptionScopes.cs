using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Complete copied scope commands. Rust derives reviewed conventions,
/// underlyings, subscriptions, scope membership, claims, expiry and recovery.
/// This adapter keeps no lease token, context graph, timer or admission lock.</summary>
public sealed class NativeOwnedOptionScopes(NativeMarketDataRuntime runtime,NativeOwnerGateway gateway,
    NativeOwnerBroker broker,NativeOwnerReferencePolicy policy,string deploymentScope)
{
    /// <summary>Submits complete reference facts and scope intent in one FFI call.
    /// Cancellation after acceptance ends this observer, not native desired work.</summary>
    public async Task AcquireAsync(NativeOwnerRawScope intent,FuturesOptionContractReadModel[] contracts,
        DateOnly valueDate,DateOnly definitionDate,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(intent);cancellationToken.ThrowIfCancellationRequested();
        runtime.ConfigureDatasetOwnerRawCatalog(valueDate,definitionDate,deploymentScope,gateway,broker,policy,
            contracts,scopeClaim:intent);
        await runtime.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,true,cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Withdraws only the stable business owner's scope demand, then
    /// observes native teardown or readiness of remaining shared workload.</summary>
    public async Task ReleaseAsync(Guid owner,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();runtime.DisableDatasetOwner(owner);
        await runtime.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false,cancellationToken).ConfigureAwait(false);
    }
}
