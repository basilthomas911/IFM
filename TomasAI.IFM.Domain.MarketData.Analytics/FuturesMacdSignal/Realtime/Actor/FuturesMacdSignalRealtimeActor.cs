using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;

/// <summary>Provides the FuturesMacdSignalRealtimeActor implementation.</summary>
public class FuturesMacdSignalRealtimeActor(
    IRealtimeActorContext<FuturesMacdSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesMacdSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesMacdSignalRealtimeContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesMacdSignalRealtimeContext, nameof(actorContext))!;

    public const string ActorName = FuturesMacdSignalSampledRealtimeEvent.Actor;
    static readonly ActorTypeId ObservationRoute = new(
        ActorType.Realtime,
        FuturesAnalyticsObservationClosedRealtimeEvent.Actor,
        FuturesAnalyticsObservationClosedRealtimeEvent.Verb);
    readonly FuturesMacdSignalRealtimeState _state = new();
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> Parsers = new()
    {
        [FuturesMacdSignalSampledRealtimeEvent.Verb] = message => message.AsEvent<FuturesMacdSignalSampledRealtimeEvent>()!,
        [FuturesMacdSignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedEvent>()!,
        [FuturesMacdSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedCompleteEvent>()!,
        [FuturesMacdSignalGeneratedFailEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedFailEvent>()!
    };

    protected override async ValueTask OnStartup(IEventActorContext<FuturesMacdSignalRealtimeActor> context)
    {
        context.AddRealtimeRouter(ObservationRoute, Id);
        await actorContext.Projector.StartAsync(context).ConfigureAwait(false);
    }

    protected override async ValueTask OnShutdown(IEventActorContext<FuturesMacdSignalRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(ObservationRoute, Id);
        await actorContext.Projector.StopAsync().ConfigureAwait(false);
    }

    protected override IEvent ParseMessage(IEventActorContext<FuturesMacdSignalRealtimeActor> context, IActorMessage message)
    {
        var subject = message.Subject;
        if (subject.Is(ActorType.Realtime, FuturesAnalyticsObservationClosedRealtimeEvent.Actor,
                FuturesAnalyticsObservationClosedRealtimeEvent.Verb))
            return message.AsEvent<FuturesAnalyticsObservationClosedRealtimeEvent>()!;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName }
            || !Parsers.TryGetValue(subject.Verb, out var parser))
            return default!;
        var @event = parser(message);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesMacdSignalRealtimeActor> context, IEvent @event)
    {
        var dispatchContext = context;
        switch (@event)
        {
            case FuturesAnalyticsObservationClosedRealtimeEvent closed:
                foreach (var entityId in FuturesAnalyticsObservationAttachmentRegistry<FuturesMacdSignalEntityId>.Snapshot()
                             .Where(entityId => Matches(entityId, closed.Observation)))
                {
                    _ = await CreateSample(entityId, closed)
                        .ExecuteAsync(actorContext.Projector, _state, actorContext.Logger).ConfigureAwait(false);
                }
                break;
            case FuturesMacdSignalSampledRealtimeEvent sampled:
                _ = await sampled.ExecuteAsync(actorContext.Projector, _state, actorContext.Logger).ConfigureAwait(false);
                break;
            case FuturesMacdSignalGeneratedFailEvent failed:
                actorContext.Logger.LogError("{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    failed.EventName, failed.EntityId, failed.ErrorMessage);
                break;
            case FuturesMacdSignalGeneratedEvent:
            case FuturesMacdSignalGeneratedCompleteEvent:
                break;
            default:
                throw new InvalidOperationException($"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        }
    }

    static bool Matches(FuturesMacdSignalEntityId entityId, FuturesAnalyticsObservationReadModel observation) =>
        StringComparer.Ordinal.Equals(entityId.ContractId, observation.ContractId)
        && entityId.ValueDate == observation.ValueDate
        && entityId.TimePeriod == observation.TimeFrame
        && observation.IsValid;

    static FuturesMacdSignalSampledRealtimeEvent CreateSample(
        FuturesMacdSignalEntityId entityId,
        FuturesAnalyticsObservationClosedRealtimeEvent closed) => new()
    {
        Subject = new(ActorType.Realtime, ActorName, FuturesMacdSignalSampledRealtimeEvent.Verb, entityId.Format()),
        Id = Guid.NewGuid(),
        EntityId = entityId,
        CommandId = closed.CommandId == Guid.Empty ? closed.Id : closed.CommandId,
        AggregateId = entityId.Format(),
        EventSource = closed.EventName,
        ReceivedOn = DateTime.UtcNow,
        FuturesPrice = closed.Observation.Close,
        SourceSequence = closed.Observation.LastSourceSequence,
        SourceEventTimestamp = closed.Observation.LastMarketEventUtc.UtcDateTime,
        Observation = closed.Observation
    };

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesMacdSignalRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
