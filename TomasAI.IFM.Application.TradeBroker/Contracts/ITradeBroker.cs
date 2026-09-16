namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>The sole actor-facing order/account broker boundary.</summary>
public interface ITradeBroker
{
    BrokerEnvironment Environment { get; }
    string AccountAlias { get; }
    long Generation { get; }
    ValueTask<BrokerDispatchReceipt> PlaceAsync(BrokerOrderRequest request, CancellationToken cancellationToken = default);
    ValueTask<BrokerDispatchReceipt> ModifyLimitAsync(BrokerLimitUpdate request, CancellationToken cancellationToken = default);
    ValueTask<BrokerDispatchReceipt> CancelAsync(BrokerCancelRequest request, CancellationToken cancellationToken = default);
    ValueTask<BrokerObservation[]> ReconcileOrderAsync(string brokerOrderId, CancellationToken cancellationToken = default);
    ValueTask<BrokerAccountSnapshot> GetAccountSnapshotAsync(CancellationToken cancellationToken = default);
    ValueTask<BrokerAccountSnapshot> ResynchronizeAccountAsync(CancellationToken cancellationToken = default);
    ValueTask<int> PublishMarketQuoteAsync(BrokerMarketQuote quote, CancellationToken cancellationToken = default);
    IAsyncEnumerable<BrokerObservation> ObserveOrdersAsync(CancellationToken cancellationToken = default);
    IAsyncEnumerable<BrokerObservation> ObserveAccountAsync(CancellationToken cancellationToken = default);
}
