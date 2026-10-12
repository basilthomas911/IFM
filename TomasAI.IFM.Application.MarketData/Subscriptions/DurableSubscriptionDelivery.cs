using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;
/// <summary>Runs storage/worker I/O outside the coordinator pump. Delivery is replayable after any uncertain outcome.</summary>
public sealed class DurableSubscriptionDelivery(IDurableSubscriptionIntentStore store) : IDisposable
{
    readonly SemaphoreSlim delivery = new(1, 1);
    public async Task<DurableRealization> ReconcileAsync(MarketDataSubscriptionCoordinator coordinator, Func<DesiredSubscriptionManifest, CancellationToken, Task<DurableRealization>> realize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(realize);
        await delivery.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var identity = coordinator.Current;
            var snapshot = await store.ReadAsync(identity.Scope, identity.Dataset, cancellationToken).ConfigureAwait(false);
            if (!await coordinator.ApplyDurableAsync(snapshot).ConfigureAwait(false))
                throw new InvalidOperationException("Committed ownership could not be installed atomically.");
            var manifest = coordinator.Current;
            var receipt = await realize(manifest, cancellationToken).ConfigureAwait(false);
            if (!receipt.AllRoutesReady || receipt.GenerationId == Guid.Empty || receipt.Revision != manifest.Revision)
                return receipt with
                {
                    AllRoutesReady = false
                };
            // A concurrent business mutation must not be mistaken for delivery of this older snapshot.
            var latest = await store.ReadAsync(identity.Scope, identity.Dataset, cancellationToken).ConfigureAwait(false);
            if (latest.Revision != snapshot.Revision || !string.Equals(coordinator.Current.Digest, manifest.Digest, StringComparison.Ordinal))
                return receipt with
                {
                    AllRoutesReady = false
                };
            var pending = await store.ReadPendingOutboxAsync(identity.Scope, identity.Dataset, 100, cancellationToken).ConfigureAwait(false);
            foreach (var item in pending.Where(x => x.Revision <= snapshot.Revision))
                if (!await store.AcknowledgeOutboxAsync(identity.Scope, identity.Dataset, item.TransitionId, cancellationToken).ConfigureAwait(false))
                    throw new IOException("Durable ownership delivery acknowledgement was not committed.");
            return receipt;
        }
        finally
        {
            delivery.Release();
        }
    }

    /// <summary>
    /// Commits every selected leg in one PostgreSQL mutation, realizes the committed union, then releases discovery.
    /// The mutation must come from a committed business-source adapter, never from a public lease request.
    /// </summary>
    public async Task<DurableIntentResult> HandoffAsync(BusinessSubscriptionSourceReference source, ICommittedBusinessSubscriptionSource authority, MarketDataSubscriptionCoordinator coordinator, Func<DesiredSubscriptionManifest, CancellationToken, Task<DurableRealization>> realize, Func<CancellationToken, Task> releaseDiscovery, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(coordinator);
        ArgumentNullException.ThrowIfNull(releaseDiscovery);
        ArgumentNullException.ThrowIfNull(source);
        var fact = DurableSubscriptionContract.Freeze(await authority.ReadAsync(source, cancellationToken).ConfigureAwait(false) ?? throw new InvalidOperationException("Committed business authority is unavailable; discovery must be retained."));
        var required = source.Kind switch
        {
            BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow => Contracts.SubscriptionLeasePurpose.Strategy,
            BusinessSubscriptionSourceKind.TradeOrder => Contracts.SubscriptionLeasePurpose.WorkingOrder,
            BusinessSubscriptionSourceKind.TradePosition => Contracts.SubscriptionLeasePurpose.Position,
            _ => throw new ArgumentException("Unsupported business authority.", nameof(source))};
        if (fact.SourceVersion != source.Version || fact.SourceEventId != source.EventId || !string.Equals(fact.Owner.WorkflowId, source.EntityId, StringComparison.Ordinal) || fact.Adds.Any(x => x.Purpose != required))
            throw new InvalidDataException("Committed business source identity or ownership purpose differs from the handoff.");
        if (fact.Status != DurableAuthorityStatus.Active || fact.Adds.Count == 0 || fact.Releases.Count != 0 || !string.Equals(fact.Scope, coordinator.Current.Scope, StringComparison.Ordinal) || !string.Equals(fact.Dataset, coordinator.Current.Dataset, StringComparison.Ordinal))
            throw new ArgumentException("Handoff requires one complete authoritative selected-leg acquisition.", nameof(source));
        var committed = await store.ApplyAsync(fact, cancellationToken).ConfigureAwait(false);
        if (committed.Code is not (DurableIntentResultCode.Committed or DurableIntentResultCode.AlreadyApplied))
            return committed;
        var realized = await ReconcileAsync(coordinator, realize, cancellationToken).ConfigureAwait(false);
        if (!realized.AllRoutesReady)
            throw new InvalidOperationException("Selected ownership is durable but not ready; discovery must be retained.");
        await releaseDiscovery(cancellationToken).ConfigureAwait(false);
        return committed;
    }

    private int disposedGateOwner;
    /// <summary>Releases the owned synchronization gate after the host has stopped its operations.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposedGateOwner, 1) != 0)
            return;
        delivery.Dispose();
        GC.SuppressFinalize(this);
    }
}