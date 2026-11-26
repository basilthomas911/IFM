namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>Provider-neutral outbound execution and inbound order evidence.</summary>
public interface IFrameworkOrderExecutionBroker
{
    string AccountAlias { get; }
    long Generation { get; }
    ValueTask<FrameworkDispatchReceipt> PlaceAsync(FrameworkOrderRequest request, CancellationToken cancellationToken = default);
    ValueTask<FrameworkDispatchReceipt> ModifyLimitAsync(FrameworkLimitUpdate request, CancellationToken cancellationToken = default);
    ValueTask<FrameworkDispatchReceipt> CancelAsync(FrameworkCancelRequest request, CancellationToken cancellationToken = default);
    ValueTask<FrameworkBrokerObservation[]> ReconcileAsync(string brokerOrderId, CancellationToken cancellationToken = default);
    ValueTask<int> PublishMarketQuoteAsync(FrameworkMarketQuote quote, CancellationToken cancellationToken = default);
    IAsyncEnumerable<FrameworkBrokerObservation> ObserveAsync(CancellationToken cancellationToken = default);
    bool HasRecoverableOrderObservations => false;
    ValueTask AcknowledgeOrderObservationAsync(Guid observationId, CancellationToken cancellationToken = default)
        => ValueTask.FromException(new NotSupportedException("Recoverable broker observations are unavailable."));
}
