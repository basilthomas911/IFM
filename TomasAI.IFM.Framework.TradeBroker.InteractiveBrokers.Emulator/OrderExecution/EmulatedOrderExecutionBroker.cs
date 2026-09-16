using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.OrderExecution;

/// <summary>Order mutation port backed by the shared synthetic ledger.</summary>
public sealed class EmulatedOrderExecutionBroker(EmulatorLedger ledger) : IFrameworkOrderExecutionBroker
{
    public string AccountAlias => ledger.AccountAlias;
    public long Generation => ledger.Generation;
    public ValueTask<FrameworkDispatchReceipt> PlaceAsync(FrameworkOrderRequest request, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(ledger.Place(request)); }
    public ValueTask<FrameworkDispatchReceipt> ModifyLimitAsync(FrameworkLimitUpdate request, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(ledger.Modify(request)); }
    public ValueTask<FrameworkDispatchReceipt> CancelAsync(FrameworkCancelRequest request, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(ledger.Cancel(request)); }
    public ValueTask<FrameworkBrokerObservation[]> ReconcileAsync(string brokerOrderId, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(ledger.Reconcile(brokerOrderId)); }
    public ValueTask<int> PublishMarketQuoteAsync(FrameworkMarketQuote quote, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return ValueTask.FromResult(ledger.PublishQuote(new EmulatorQuote(quote.ContractId, quote.Bid, quote.Ask, quote.BidSize, quote.AskSize, quote.MarketTimeUtc, quote.SourceEpoch, quote.SourceSequence))); }
    public IAsyncEnumerable<FrameworkBrokerObservation> ObserveAsync(CancellationToken cancellationToken = default) => ledger.ObserveOrders(cancellationToken);
}
