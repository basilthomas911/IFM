using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Command.EventProjector;

/// <summary>
/// Compatibility projector for legacy snapshot commands. Current Market Outlook publication uses
/// latest-value persistence and does not enqueue display snapshots in this projector.
/// </summary>
public sealed class MarketOutlookSnapshotEventProjector
    : BaseEventProjector<MarketOutlookSnapshotCommandActor>
{
    readonly IDbContextFactory dbFactory;
    readonly ImmutableArray<EventProjectionDescriptor> descriptors;

    public MarketOutlookSnapshotEventProjector(
        IDbContextFactory dbFactory,
        IDurableReplayQueue durableReplayQueue,
        IEventSourceActorDbContext eventSource,
        IBlackboardService blackboard,
        ILogger<MarketOutlookSnapshotEventProjector> logger,
        EventProjectorReliabilityOptions? reliabilityOptions = null)
        : base(durableReplayQueue, eventSource, blackboard, logger, reliabilityOptions)
    {
        this.dbFactory = dbFactory;
        descriptors =
        [
            new(
            typeof(MarketOutlookSnapshotInsertedEvent),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation,
            ApplyAndPublishAsync,
            _ => null,
            (_, _) => null,
            publishProcessingEvent: false,
            useDurableReplay: false,
            publishTerminalEvent: false)
        ];
    }

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
    public override string ActorName => nameof(MarketOutlookSnapshotCommandActor);
    public override string ProjectorName => nameof(MarketOutlookSnapshotEventProjector);
    public override string DurableProcessQueueName => $"{ActorName}.{ProjectorName}.ProcessQueue";
    public override string DurableReplayQueueName => $"{ActorName}.{ProjectorName}.ReplayQueue";

    async ValueTask<EventProjectionApplyResult> ApplyAndPublishAsync(
        IEvent domainEvent,
        ProjectionExecutionContext execution)
    {
        var inserted = (MarketOutlookSnapshotInsertedEvent)domainEvent;
        return await ApplyAndPublishAsync(
            inserted, execution, dbFactory.MarketDataDb, Context).ConfigureAwait(false);
    }

    internal static async ValueTask<EventProjectionApplyResult> ApplyAndPublishAsync(
        MarketOutlookSnapshotInsertedEvent inserted,
        ProjectionExecutionContext execution,
        IMarketDataDbContext marketDataDb,
        ICommandActorContext actorContext)
    {
        await marketDataDb.UpsertMarketOutlookSnapshotAsync(
            inserted.MarketOutlook, execution.EventId, execution.CancellationToken)
            .ConfigureAwait(false);
        var realtime = inserted with
        {
            Subject = new ActorSubject(
                ActorType.Realtime,
                MarketOutlookSnapshotInsertedEvent.Actor,
                MarketOutlookSnapshotInsertedEvent.Verb,
                inserted.EntityId.Format())
        };
        await actorContext.SendAsync<MarketOutlookSnapshotInsertedEvent, MarketOutlookEntityId>(
            realtime, execution.CancellationToken).ConfigureAwait(false);
        return new EventProjectionApplyResult(EventProjectionApplyOutcome.Applied);
    }
}
