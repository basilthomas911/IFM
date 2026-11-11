namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>Qualifies durable subscription requests before invoking the persistence adapter.</summary>
public sealed class DurableSubscriptionIntentStore(IDurableSubscriptionPersistence persistence) : IDurableSubscriptionIntentStore
{
    /// <inheritdoc/>
    public async Task<DurableSubscriptionSnapshot> ReadAsync(string scope, string dataset, CancellationToken cancellationToken = default)
    {
        DurableSubscriptionContract.ValidateScope(scope, dataset);
        return await persistence.ReadAsync(scope, dataset, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<DurableIntentResult?> FindOperationAsync(string scope, string dataset, Guid operationId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(scope, dataset, operationId);
        return await persistence.FindOperationAsync(scope, dataset, operationId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<DurableIntentResult> ApplyAsync(DurableAuthorityMutation mutation, CancellationToken cancellationToken = default)
        => await persistence.CommitAsync(DurableSubscriptionCommand.Prepare(mutation), cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DurableSubscriptionOutboxItem>> ReadPendingOutboxAsync(string scope, string dataset,
        int pageSize = 100, CancellationToken cancellationToken = default)
    {
        DurableSubscriptionContract.ValidateScope(scope, dataset);
        if (pageSize is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(pageSize));
        return await persistence.ReadPendingOutboxAsync(scope, dataset, pageSize, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> AcknowledgeOutboxAsync(string scope, string dataset, Guid transitionId,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentity(scope, dataset, transitionId);
        return await persistence.AcknowledgeOutboxAsync(scope, dataset, transitionId, cancellationToken).ConfigureAwait(false);
    }

    static void ValidateIdentity(string scope, string dataset, Guid id)
    {
        DurableSubscriptionContract.ValidateScope(scope, dataset);
        if (id == Guid.Empty) throw new ArgumentException("A non-empty identifier is required.", nameof(id));
    }
}
