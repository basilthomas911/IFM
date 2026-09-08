using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;

/// <summary>Worker acknowledgement of one complete committed intent revision, including all selected legs.</summary>
public sealed record DurableRealization(long Revision, Guid GenerationId, bool AllRoutesReady);

/// <summary>Runs storage/worker I/O outside the coordinator pump. Delivery is replayable after any uncertain outcome.</summary>
public sealed class DurableSubscriptionDelivery(IDurableSubscriptionIntentStore store)
{
    readonly SemaphoreSlim delivery = new(1, 1);

    public async Task<DurableRealization> ReconcileAsync(MarketDataSubscriptionCoordinator coordinator,
        Func<DesiredSubscriptionManifest, CancellationToken, Task<DurableRealization>> realize,
        CancellationToken cancellationToken)
    {
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
                return receipt with { AllRoutesReady = false };
            // A concurrent business mutation must not be mistaken for delivery of this older snapshot.
            var latest = await store.ReadAsync(identity.Scope, identity.Dataset, cancellationToken).ConfigureAwait(false);
            if (latest.Revision != snapshot.Revision || coordinator.Current.Digest != manifest.Digest)
                return receipt with { AllRoutesReady = false };
            var pending = await store.ReadPendingOutboxAsync(identity.Scope, identity.Dataset, 100, cancellationToken).ConfigureAwait(false);
            foreach (var item in pending.Where(x => x.Revision <= snapshot.Revision))
                if (!await store.AcknowledgeOutboxAsync(identity.Scope, identity.Dataset, item.TransitionId, cancellationToken).ConfigureAwait(false))
                    throw new IOException("Durable ownership delivery acknowledgement was not committed.");
            return receipt;
        }
        finally { delivery.Release(); }
    }

    /// <summary>
    /// Commits every selected leg in one PostgreSQL mutation, realizes the committed union, then releases discovery.
    /// The mutation must come from a committed business-source adapter, never from a public lease request.
    /// </summary>
    public async Task<DurableIntentResult> HandoffAsync(BusinessSubscriptionSourceReference source,
        ICommittedBusinessSubscriptionSource authority,
        MarketDataSubscriptionCoordinator coordinator,
        Func<DesiredSubscriptionManifest, CancellationToken, Task<DurableRealization>> realize,
        Func<CancellationToken, Task> releaseDiscovery, CancellationToken cancellationToken)
    {
        var fact = DurableSubscriptionContract.Freeze(await authority.ReadAsync(source, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Committed business authority is unavailable; discovery must be retained."));
        var required = source.Kind switch
        {
            BusinessSubscriptionSourceKind.IntrinsicTimeWorkflow => Contracts.SubscriptionLeasePurpose.Strategy,
            BusinessSubscriptionSourceKind.TradeOrder => Contracts.SubscriptionLeasePurpose.WorkingOrder,
            BusinessSubscriptionSourceKind.TradePosition => Contracts.SubscriptionLeasePurpose.Position,
            _ => throw new ArgumentException("Unsupported business authority.")
        };
        if (fact.SourceVersion != source.Version || fact.SourceEventId != source.EventId
            || fact.Owner.WorkflowId != source.EntityId || fact.Adds.Any(x => x.Purpose != required))
            throw new InvalidDataException("Committed business source identity or ownership purpose differs from the handoff.");
        if (fact.Status != DurableAuthorityStatus.Active || fact.Adds.Count == 0 || fact.Releases.Count != 0
            || fact.Scope != coordinator.Current.Scope || fact.Dataset != coordinator.Current.Dataset)
            throw new ArgumentException("Handoff requires one complete authoritative selected-leg acquisition.");
        var committed = await store.ApplyAsync(fact, cancellationToken).ConfigureAwait(false);
        if (committed.Code is not (DurableIntentResultCode.Committed or DurableIntentResultCode.AlreadyApplied)) return committed;
        var realized = await ReconcileAsync(coordinator, realize, cancellationToken).ConfigureAwait(false);
        if (!realized.AllRoutesReady) throw new InvalidOperationException("Selected ownership is durable but not ready; discovery must be retained.");
        await releaseDiscovery(cancellationToken).ConfigureAwait(false);
        return committed;
    }
}
