namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>Persists validated durable intent selections and commits prepared commands atomically.</summary>
public interface IDurableSubscriptionPersistence
{
    Task<DurableSubscriptionSnapshot> ReadAsync(string scope, string dataset, CancellationToken cancellationToken = default);
    Task<DurableIntentResult?> FindOperationAsync(string scope, string dataset, Guid operationId,
        CancellationToken cancellationToken = default);
    Task<DurableIntentResult> CommitAsync(DurableSubscriptionCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingOutboxAsync(string scope, string dataset,
        int pageSize = 100, CancellationToken cancellationToken = default);
    Task<bool> AcknowledgeOutboxAsync(string scope, string dataset, Guid transitionId,
        CancellationToken cancellationToken = default);
}
