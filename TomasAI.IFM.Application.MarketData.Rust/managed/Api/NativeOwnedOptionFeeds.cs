using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Thin monitored-feed command adapter. The retained fields are copied
/// endpoint/policy configuration and a command/query runtime; no managed claim,
/// lease, connection, renewal, reference preparation or recovery task lives here.</summary>
public sealed class NativeOwnedOptionFeeds(NativeMarketDataRuntime runtime,NativeOwnerGateway gateway,
    NativeOwnerBroker broker,NativeOwnerReferencePolicy policy,string scope)
{
    /// <summary>Accepts one durable workflow/leg demand. Rust derives metadata,
    /// source dependencies and the subscription plan, and owns the entire feed.
    /// Cancelling the subsequent readiness observer cannot withdraw this intent.</summary>
    public void Accept(TickerStreamOwner owner,FuturesOptionContractReadModel contract,DateOnly valueDate,DateOnly definitionDate)
    {
        ArgumentNullException.ThrowIfNull(contract);
        runtime.ConfigureDatasetOwnerRawCatalog(valueDate,definitionDate,scope,gateway,broker,policy,[contract],owner);
    }
    /// <summary>Accepts canonical demand before database/provider work. Rust owns
    /// lookup, cancellation, reconstruction and its original recovery deadline.</summary>
    public void AcceptCanonical(TickerStreamOwner owner,string contract,DateOnly valueDate,DateOnly definitionDate)
        =>runtime.ConfigureDatasetOwnerRawCatalog(valueDate,definitionDate,scope,gateway,broker,policy,[],canonicalOwner:owner,canonicalContract:contract);
    /// <summary>Accepts demand then awaits event-driven native readiness under
    /// Rust's original dataset deadline. There is no independent C# timeout.
    /// Caller cancellation ends its wait only; explicit release ends demand.</summary>
    public async Task<bool> AcquireAsync(TickerStreamOwner owner,FuturesOptionContractReadModel contract,DateOnly valueDate,DateOnly definitionDate,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Accept(owner,contract,valueDate,definitionDate);
        await runtime.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,true,cancellationToken).ConfigureAwait(false);
        return true;
    }
    /// <summary>Releases demand and observes completion in the native owner.
    /// A remaining shared workload must become ready; an empty dataset must finish
    /// native teardown. The observer owns no lease and adds no recovery timeout.</summary>
    public async Task<bool> ReleaseAsync(TickerStreamOwner owner,string contract,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        runtime.ReleaseDatasetOwnerOption(owner,contract);
        await runtime.WaitDatasetOwnerReadyAsync(MarketDataLane.OptionsGlbx,false,cancellationToken).ConfigureAwait(false);
        return true;
    }
    /// <summary>Releases one exact business identity. Duplicate release is safe;
    /// shared underlyings and other workflows remain native-owned and active.</summary>
    public bool Release(TickerStreamOwner owner,string contract)
    {
        runtime.ReleaseDatasetOwnerOption(owner,contract);return true;
    }
}
