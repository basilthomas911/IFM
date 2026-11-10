using System.Runtime.CompilerServices;
using TomasAI.IFM.Application.TradeBroker.Contracts;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Isolated transport fault: the real emulator persists its orders/fills while acknowledgements and lookup replies are unavailable.</summary>
public sealed class LostOpeningBrokerReplies(ITradeBroker inner) : ITradeBroker
{
    public BrokerEnvironment Environment => inner.Environment;
    public string AccountAlias => inner.AccountAlias;
    public long Generation => inner.Generation;
    public BrokerCapabilities Capabilities => inner.Capabilities;

    public async ValueTask<BrokerDispatchReceipt> PlaceAsync(BrokerOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (Environment != BrokerEnvironment.Emulator) throw new InvalidOperationException("Fault injection is restricted to the Development emulator.");
        var result = await inner.PlaceAsync(request, cancellationToken);
        return result.Outcome == BrokerDispatchOutcome.AcceptedForDispatch
            ? result with { Outcome = BrokerDispatchOutcome.OutcomeUnknown, Category = "TEST.REPLY_LOST", Detail = "Isolated qualification: accepted Place reply deliberately unavailable." }
            : result;
    }

    public ValueTask<BrokerDispatchReceipt> ModifyLimitAsync(BrokerLimitUpdate request, CancellationToken cancellationToken = default) => inner.ModifyLimitAsync(request, cancellationToken);
    public ValueTask<BrokerDispatchReceipt> CancelAsync(BrokerCancelRequest request, CancellationToken cancellationToken = default) => inner.CancelAsync(request, cancellationToken);
    public ValueTask<BrokerObservation[]> ReconcileOrderAsync(string brokerOrderId, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(Array.Empty<BrokerObservation>()); }
    public ValueTask<BrokerAccountSnapshot> GetAccountSnapshotAsync(CancellationToken cancellationToken = default) => inner.GetAccountSnapshotAsync(cancellationToken);
    public ValueTask<BrokerAccountSnapshot> ResynchronizeAccountAsync(CancellationToken cancellationToken = default) => inner.ResynchronizeAccountAsync(cancellationToken);
    public ValueTask<int> PublishMarketQuoteAsync(BrokerMarketQuote quote, CancellationToken cancellationToken = default) => inner.PublishMarketQuoteAsync(quote, cancellationToken);
    public async IAsyncEnumerable<BrokerObservation> ObserveOrdersAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var observation in inner.ObserveOrdersAsync(cancellationToken))
            _ = observation; // Delivery is lost; the underlying immutable emulator journal is retained.
        yield break;
    }
    public IAsyncEnumerable<BrokerObservation> ObserveAccountAsync(CancellationToken cancellationToken = default) => inner.ObserveAccountAsync(cancellationToken);
}
