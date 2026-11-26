using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.Api.Server.Core.Trading.Emulation;

/// <summary>Dispatches emulator orders; timer fills require the original fresh executable quote book.</summary>
public sealed class FrozenEmulatorOrderExecutionBroker(
    IFrameworkOrderExecutionBroker inner,
    IFuturesMarketSessionAuthority marketSession,
    IEmulatorClock clock,
    OfflineFillSimulation? simulation = null) : IFrameworkOrderExecutionBroker
{
    public string AccountAlias => inner.AccountAlias;
    public long Generation => inner.Generation;
    public bool HasRecoverableOrderObservations => inner.HasRecoverableOrderObservations;
    public ValueTask AcknowledgeOrderObservationAsync(Guid observationId, CancellationToken cancellationToken = default)
        => inner.AcknowledgeOrderObservationAsync(observationId, cancellationToken);

    public async ValueTask<FrameworkDispatchReceipt> PlaceAsync(FrameworkOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(marketSession);
        ArgumentNullException.ThrowIfNull(clock);
        var receipt = await inner.PlaceAsync(request, cancellationToken).ConfigureAwait(false);
        if (receipt.Outcome == FrameworkDispatchOutcome.AcceptedForDispatch && simulation is { Enabled: true })
        {
            simulation.Start(request.BrokerOrderId);
            return receipt;
        }
        // Frozen-chain previews are display evidence, not executable live quotes. In particular, never
        // refresh their timestamps or invent size to make an unattended closing order fill.
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
