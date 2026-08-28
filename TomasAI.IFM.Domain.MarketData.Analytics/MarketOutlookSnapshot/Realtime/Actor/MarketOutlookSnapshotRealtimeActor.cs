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

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [MarketOutlookComponentChangedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentChangedRealtimeEvent>()!,
            [MarketOutlookEodUpdatedRealtimeEvent.Verb] =
                message => message.AsEvent<MarketOutlookEodUpdatedRealtimeEvent>()!,
            [MarketOutlookComponentObservedCompleteEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentObservedCompleteEvent>()!,
            [MarketOutlookComponentObservedFailEvent.Verb] =
                message => message.AsEvent<MarketOutlookComponentObservedFailEvent>()!,
            [MarketOutlookSnapshotPublishedCompleteEvent.Verb] =
                message => message.AsEvent<MarketOutlookSnapshotPublishedCompleteEvent>()!,
            [MarketOutlookSnapshotPublishedFailEvent.Verb] =
                message => message.AsEvent<MarketOutlookSnapshotPublishedFailEvent>()!
        };

    readonly IReadOnlyDictionary<Type, Func<IEvent, IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>, ValueTask>>
        _receiveMap = new Dictionary<Type, Func<IEvent, IRealtimeActorContext<MarketOutlookSnapshotRealtimeActor>, ValueTask>>
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
        => ParseMappedRealtimeEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<MarketOutlookSnapshotRealtimeActor> context,
        IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
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
