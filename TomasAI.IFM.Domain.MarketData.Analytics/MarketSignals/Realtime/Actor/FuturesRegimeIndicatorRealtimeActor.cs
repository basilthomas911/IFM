using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.Actor;

/// <summary>
/// Calculates the MDSI-7 through MDSI-10 signals in one ordered mailbox from each shared observation.
/// </summary>
public sealed class FuturesRegimeIndicatorRealtimeActor(
    IRealtimeActorContext<FuturesRegimeIndicatorRealtimeActor> actorContext)
    : BaseEventActor<FuturesRegimeIndicatorRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the realtime mailbox name.</summary>
    public const string ActorName = FuturesRegimeIndicatorsGeneratedRealtimeEvent.Actor;

    static readonly ActorTypeId ObservationRoute = new(
        ActorType.Realtime,
        FuturesAnalyticsObservationClosedRealtimeEvent.Actor,
        FuturesAnalyticsObservationClosedRealtimeEvent.Verb);
    readonly FuturesRegimeIndicatorPipelineRealtimeState state = new();

    /// <summary>Registers the shared-observation route and starts storage projection.</summary>
    protected override async ValueTask OnStartup(
        IEventActorContext<FuturesRegimeIndicatorRealtimeActor> context)
    {
        context.AddRealtimeRouter(ObservationRoute, Id);
        await actorContext.Projector.StartAsync(context).ConfigureAwait(false);
    }

    /// <summary>Releases the route, cache, and projector in reverse order.</summary>
    protected override async ValueTask OnShutdown(
        IEventActorContext<FuturesRegimeIndicatorRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(ObservationRoute, Id);
        FuturesRegimeIndicatorSnapshotCache.Clear();
        await actorContext.Projector.StopAsync().ConfigureAwait(false);
    }

    /// <summary>Parses shared observations and this actor's projection lifecycle events.</summary>
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesRegimeIndicatorRealtimeActor> context,
        IActorMessage message)
    {
        var subject = message.Subject;
        if (subject.Is(ActorType.Realtime, FuturesAnalyticsObservationClosedRealtimeEvent.Actor,
                FuturesAnalyticsObservationClosedRealtimeEvent.Verb))
            return message.AsEvent<FuturesAnalyticsObservationClosedRealtimeEvent>()!;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName })
            return default!;
        return subject.Verb switch
        {
            FuturesRegimeIndicatorsGeneratedRealtimeEvent.Verb =>
                message.AsEvent<FuturesRegimeIndicatorsGeneratedRealtimeEvent>()!,
            FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent.Verb =>
                message.AsEvent<FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent>()!,
            FuturesRegimeIndicatorsGeneratedFailRealtimeEvent.Verb =>
                message.AsEvent<FuturesRegimeIndicatorsGeneratedFailRealtimeEvent>()!,
            _ => default!
        };
    }

    /// <summary>Calculates and projects each unique observation exactly once.</summary>
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesRegimeIndicatorRealtimeActor> context,
        IEvent @event)
    {
        switch (@event)
        {
            case FuturesAnalyticsObservationClosedRealtimeEvent closed when closed.Observation.IsValid:
            {
                var snapshot = state.Apply(closed.Observation);
                var generated = new FuturesRegimeIndicatorsGeneratedRealtimeEvent
                {
                    Subject = new(ActorType.Realtime, ActorName,
                        FuturesRegimeIndicatorsGeneratedRealtimeEvent.Verb, closed.EntityId.Format()),
                    Id = Guid.NewGuid(),
                    EntityId = closed.EntityId,
                    CommandId = closed.CommandId == Guid.Empty ? closed.Id : closed.CommandId,
                    AggregateId = closed.EntityId.Format(),
                    EventSource = closed.EventName,
                    ReceivedOn = DateTime.UtcNow,
                    Snapshot = snapshot
                };
                if (await actorContext.Projector.ProcessRealtimeEventAsync(generated).ConfigureAwait(false))
                    FuturesRegimeIndicatorSnapshotCache.Set(closed.EntityId, snapshot);
                break;
            }
            case FuturesRegimeIndicatorsGeneratedFailRealtimeEvent failed:
                actorContext.Logger.LogError(
                    "{EventName} for {EntityId}: {ErrorMessage}; no replay will be attempted",
                    failed.EventName, failed.EntityId, failed.ErrorMessage);
                break;
            case FuturesAnalyticsObservationClosedRealtimeEvent:
            case FuturesRegimeIndicatorsGeneratedRealtimeEvent:
            case FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent:
                break;
            default:
                throw new InvalidOperationException($"Unsupported regime-indicator event {@event.EventName}.");
        }
    }

    /// <summary>Reports unexpected actor failures through the standard realtime error event.</summary>
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesRegimeIndicatorRealtimeActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
