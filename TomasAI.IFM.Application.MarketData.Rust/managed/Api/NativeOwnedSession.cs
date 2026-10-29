using TomasAI.IFM.Application.MarketData.Rust.Delivery;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;

/// <summary>Copied demand/configuration for a native futures dataset. Roots are
/// resolved, subscribed and recovered by Rust, independently of the caller.</summary>
public sealed record NativeOwnedFuturesDemand(MarketDataLane Lane,NativeOwnerGateway Gateway,NativeOwnerFuturesRoot[] Roots,bool SessionReplay=true);

/// <summary>Command-only lifecycle adapter for the complete native owner. It
/// owns no provider epoch, C# lease registry, periodic renewal, stream drain,
/// pricing preparation or recovery timer. The native runtime owns the dataset
/// supervisors and their resources for the whole application session.</summary>
public sealed class NativeOwnedSession(NativeMarketDataRuntime runtime,NativeOwnerBroker broker,string scope)
{
    /// <summary>Accepts configured roots then awaits native readiness. Each
    /// dataset owns one original two-minute recovery deadline; C# does not nest
    /// another deadline around it. Caller cancellation stops observation only.</summary>
    public async Task StartAsync(DateOnly valueDate,DateOnly definitionDate,NativeOwnedFuturesDemand[] demands,CancellationToken cancellationToken=default)
    {
        ArgumentNullException.ThrowIfNull(demands);
        cancellationToken.ThrowIfCancellationRequested();
        runtime.StartDatasetOwnerFutures(valueDate,definitionDate,scope,broker,demands);
        await runtime.WaitDatasetOwnerFuturesReadyAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Explicitly withdraws desired feeds. Rust cancels and retires its
    /// groups; there is no managed queue or consumer drain prerequisite.</summary>
    public void Stop()=>runtime.StopDatasetOwnerFeeds();
    /// <summary>Withdraws feed intent and awaits native cancellation/join.
    /// Cancelling the observer cannot undo the accepted stop command.</summary>
    public async Task StopAsync(CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();runtime.StopDatasetOwnerFeeds();
        await runtime.WaitDatasetOwnerFeedsStoppedAsync(cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Requests native reconstruction. Completion and the original
    /// recovery budget are observed from the native owner, never a C# watchdog.</summary>
    public async Task RecoverAsync(MarketDataLane lane,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        runtime.RecoverDatasetOwner(lane);
        await runtime.WaitDatasetOwnerReadyAsync(lane,false,cancellationToken).ConfigureAwait(false);
    }
}
