using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>
/// Durable persistence boundary. Storage/transport exceptions leave commit outcome uncertain:
/// reconcile by OperationId. Public lease callers cannot authenticate themselves as business authorities.
/// </summary>
public interface IDurableSubscriptionIntentStore
{
    Task<DurableSubscriptionSnapshot> ReadAsync(string scope, string dataset, CancellationToken cancellationToken = default);
    Task<DurableIntentResult?> FindOperationAsync(string scope, string dataset, Guid operationId,
        CancellationToken cancellationToken = default);
    Task<DurableIntentResult> ApplyAsync(DurableAuthorityMutation mutation, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingOutboxAsync(string scope, string dataset,
        int pageSize = 100, CancellationToken cancellationToken = default);
    Task<bool> AcknowledgeOutboxAsync(string scope, string dataset, Guid transitionId,
        CancellationToken cancellationToken = default);
}
