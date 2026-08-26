using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Actor;

/// <summary>
/// Bridges Market Outlook realtime inputs to its command aggregate and publishes projected snapshot notifications.
/// </summary>
/// <remarks>This actor is stateless. PostgreSQL command events own state and ScyllaDB contains read projections.</remarks>
public class MarketOutlookSnapshotRealtimeActor(
    IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor> actorContext)
    : BaseEventActor<MarketOutlookSnapshotRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the stable actor mailbox name retained for wire compatibility.</summary>
    public const string ActorName = "MarketOutlook";

    readonly Dictionary<Type, Func<IEvent, IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>, ValueTask>>
        _receiveMap = new()
        {
            [typeof(MarketOutlookComponentChangedRealtimeEvent)] = (@event, context) =>
                ((MarketOutlookComponentChangedRealtimeEvent)@event).ObserveAsync(context),
            [typeof(MarketOutlookEodUpdatedRealtimeEvent)] = (@event, context) =>
                ((MarketOutlookEodUpdatedRealtimeEvent)@event).PublishAsync(context),
            [typeof(MarketOutlookComponentObservedCompleteEvent)] = (@event, context) =>
                ((MarketOutlookComponentObservedCompleteEvent)@event).CompleteAsync(context),
            [typeof(MarketOutlookComponentObservedFailEvent)] = (@event, context) =>
                ((MarketOutlookComponentObservedFailEvent)@event).FailAsync(context),
            [typeof(MarketOutlookSnapshotPublishedCompleteEvent)] = (@event, context) =>
                ((MarketOutlookSnapshotPublishedCompleteEvent)@event).CompleteAsync(context),
            [typeof(MarketOutlookSnapshotPublishedFailEvent)] = (@event, context) =>
                ((MarketOutlookSnapshotPublishedFailEvent)@event).FailAsync(context)
        };

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Realtime, Name: ActorName })
            return default!;
        IEvent? @event = message.Subject.Verb switch
        {
            MarketOutlookComponentChangedRealtimeEvent.Verb =>
                message.AsEvent<MarketOutlookComponentChangedRealtimeEvent>(),
            MarketOutlookEodUpdatedRealtimeEvent.Verb =>
                message.AsEvent<MarketOutlookEodUpdatedRealtimeEvent>(),
            MarketOutlookComponentObservedCompleteEvent.Verb =>
                message.AsEvent<MarketOutlookComponentObservedCompleteEvent>(),
            MarketOutlookComponentObservedFailEvent.Verb =>
                message.AsEvent<MarketOutlookComponentObservedFailEvent>(),
            MarketOutlookSnapshotPublishedCompleteEvent.Verb =>
                message.AsEvent<MarketOutlookSnapshotPublishedCompleteEvent>(),
            MarketOutlookSnapshotPublishedFailEvent.Verb =>
                message.AsEvent<MarketOutlookSnapshotPublishedFailEvent>(),
            _ => throw new InvalidOperationException(
                $"Unable to resolve {ActorName} realtime event from {message.Subject}.")
        };
        return @event ?? throw new InvalidOperationException(
            $"Unable to deserialize {ActorName} realtime event from {message.Subject}.");
    }

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IEvent @event)
    {
        if (!_receiveMap.TryGetValue(@event.GetType(), out var handler))
            throw new InvalidOperationException(
                $"Unable to dispatch {ActorName} realtime event {@event.GetType().Name}.");
        await handler(@event, (IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>)context)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override ValueTask OnExceptionAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception)
    {
        actorContext.Logger.LogErrorEvent(
            ActorName,
            exception,
            "Market Outlook realtime bridge failed for {EntityId}",
            @event.Subject.EntityId);
        return ValueTask.CompletedTask;
    }
}
