namespace TomasAI.IFM.Application.TradeBroker.Contracts;

/// <summary>The sole actor-facing order/account broker boundary.</summary>
public interface ITradeBroker
{
    BrokerEnvironment Environment { get; }
    string AccountAlias { get; }
    long Generation { get; }
    BrokerCapabilities Capabilities { get; }
    ValueTask<BrokerDispatchReceipt> PlaceAsync(BrokerOrderRequest request, CancellationToken cancellationToken = default);
    ValueTask<BrokerDispatchReceipt> ModifyLimitAsync(BrokerLimitUpdate request, CancellationToken cancellationToken = default);
    ValueTask<BrokerDispatchReceipt> CancelAsync(BrokerCancelRequest request, CancellationToken cancellationToken = default);
    ValueTask<BrokerObservation[]> ReconcileOrderAsync(string brokerOrderId, CancellationToken cancellationToken = default);
    ValueTask<BrokerAccountSnapshot> GetAccountSnapshotAsync(CancellationToken cancellationToken = default);
    ValueTask<BrokerAccountSnapshot> ResynchronizeAccountAsync(CancellationToken cancellationToken = default);
    ValueTask<int> PublishMarketQuoteAsync(BrokerMarketQuote quote, CancellationToken cancellationToken = default);
    IAsyncEnumerable<BrokerObservation> ObserveOrdersAsync(CancellationToken cancellationToken = default);
    /// <summary>Whether order observations resume from a durable publication cursor.</summary>
    bool HasRecoverableOrderObservations => false;
    /// <summary>Advance only after confirmed publication; duplicate acknowledgements are harmless.</summary>
    ValueTask AcknowledgeOrderObservationAsync(Guid observationId, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new NotSupportedException("Recoverable broker observations are unavailable."));
    IAsyncEnumerable<BrokerObservation> ObserveAccountAsync(CancellationToken cancellationToken = default);
}
