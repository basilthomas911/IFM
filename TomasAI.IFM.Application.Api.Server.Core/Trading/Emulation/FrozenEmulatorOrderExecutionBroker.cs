using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.Api.Server.Core.Trading.Emulation;

/// <summary>Matches an emulator order against a recently requested frozen chain while the market is closed.</summary>
public sealed class FrozenEmulatorOrderExecutionBroker(
    IFrameworkOrderExecutionBroker inner,
    IFuturesMarketSessionAuthority marketSession,
    IEmulatorClock clock,
    OfflineFillSimulation? simulation = null) : IFrameworkOrderExecutionBroker
{
    public string AccountAlias => inner.AccountAlias;
    public long Generation => inner.Generation;

    public async ValueTask<FrameworkDispatchReceipt> PlaceAsync(FrameworkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var receipt = await inner.PlaceAsync(request, cancellationToken).ConfigureAwait(false);
        if (receipt.Outcome == FrameworkDispatchOutcome.AcceptedForDispatch && simulation is { Enabled: true })
        {
            simulation.Start(request.BrokerOrderId);
            return receipt;
        }
        if (receipt.Outcome != FrameworkDispatchOutcome.AcceptedForDispatch
            || marketSession.Current.IsMarketOpen || request.Legs.Length is not (2 or 4)
            || request.Legs.Any(leg => leg.Strike is not > 0)) return receipt;
        var now = new DateTimeOffset(clock.UtcNow, TimeSpan.Zero);
        var quotes = new (string ContractId, decimal Bid, decimal Ask, int Size)[request.Legs.Length];
        for (var index = 0; index < request.Legs.Length; index++)
        {
            var leg = request.Legs[index];
            if (!FrozenEmulatorQuoteStore.TryGet(leg.ContractId, now, out var bid, out var ask)) return receipt;
            quotes[index] = (leg.ContractId, bid, ask, Math.Max(100, Math.Abs(leg.SignedQuantity)));
        }
        var sequence = Math.Max(1, now.UtcTicks);
        try
        {
            foreach (var quote in quotes)
                await inner.PublishMarketQuoteAsync(new(quote.ContractId, quote.Bid, quote.Ask,
                    quote.Size, quote.Size, now.UtcDateTime, Generation, sequence), cancellationToken)
                    .ConfigureAwait(false);
            Serilog.Log.Information(
                "Published {Count} frozen option quotes to emulator order {BrokerOrderId} while the market is closed.",
                quotes.Length, request.BrokerOrderId);
        }
        catch (Exception exception)
        {
            // Dispatch already succeeded. A preview matching fault must not turn that
            // accepted order into an apparent failed submission that may be retried.
            Serilog.Log.Error(exception,
                "Frozen emulator quote matching failed after order {BrokerOrderId} was accepted.",
                request.BrokerOrderId);
        }
        return receipt;
    }

    public ValueTask<FrameworkDispatchReceipt> ModifyLimitAsync(FrameworkLimitUpdate request,
        CancellationToken cancellationToken = default) => inner.ModifyLimitAsync(request, cancellationToken);
    public ValueTask<FrameworkDispatchReceipt> CancelAsync(FrameworkCancelRequest request,
        CancellationToken cancellationToken = default) => inner.CancelAsync(request, cancellationToken);
    public ValueTask<FrameworkBrokerObservation[]> ReconcileAsync(string brokerOrderId,
        CancellationToken cancellationToken = default) => inner.ReconcileAsync(brokerOrderId, cancellationToken);
    public ValueTask<int> PublishMarketQuoteAsync(FrameworkMarketQuote quote,
        CancellationToken cancellationToken = default) => inner.PublishMarketQuoteAsync(quote, cancellationToken);
    public IAsyncEnumerable<FrameworkBrokerObservation> ObserveAsync(
        CancellationToken cancellationToken = default) => inner.ObserveAsync(cancellationToken);
}
