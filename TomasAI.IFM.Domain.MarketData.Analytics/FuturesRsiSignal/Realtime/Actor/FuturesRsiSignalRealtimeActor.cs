using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;

/// <summary>Owns non-replayable intraday RSI computation and projection.</summary>
public class FuturesRsiSignalRealtimeActor(
    IRealtimeActorContext<FuturesRsiSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesRsiSignalRealtimeActor>(actorContext.Supervisor, actorContext.Logger, actorContext.ActorId)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesRsiSignalRealtimeContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesRsiSignalRealtimeContext, nameof(actorContext))!;

    public const string ActorName = FuturesRsiSignalSampledRealtimeEvent.Actor;
    readonly FuturesRsiSignalRealtimeState _state = new();

    static readonly Dictionary<string, Func<IActorMessage, IEvent>> Parsers = new()
    {
        [FuturesRsiSignalSampledRealtimeEvent.Verb] =
            message => message.AsEvent<FuturesRsiSignalSampledRealtimeEvent>()!,
        [FuturesRsiSignalGeneratedEvent.Verb] =
            message => message.AsEvent<FuturesRsiSignalGeneratedEvent>()!,
        [FuturesRsiSignalGeneratedCompleteEvent.Verb] =
            message => message.AsEvent<FuturesRsiSignalGeneratedCompleteEvent>()!,
        [FuturesRsiSignalGeneratedFailEvent.Verb] =
            message => message.AsEvent<FuturesRsiSignalGeneratedFailEvent>()!,
        [FuturesRsiSignalsGeneratedEvent.Verb] =
            message => message.AsEvent<FuturesRsiSignalsGeneratedEvent>()!
    };

    protected override ValueTask OnStartup(IEventActorContext context) => actorContext.Projector.StartAsync(context);
    protected override ValueTask OnShutdown(IEventActorContext context) => actorContext.Projector.StopAsync();

    protected override IEvent ParseMessage(IEventActorContext context, IActorMessage message)
    {
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName }
            || !Parsers.TryGetValue(subject.Verb, out var parser))
            return default!;
        var @event = parser(message);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    protected override async ValueTask ReceiveAsync(IEventActorContext context, IEvent @event)
    {
        var dispatchContext = actorContext.RouteTo(context);
        switch (@event)
        {
            case FuturesRsiSignalSampledRealtimeEvent sampled:
                _ = await sampled.ExecuteAsync(context, actorContext.Projector, _state, actorContext.Blackboard, actorContext.Logger)
                    .ConfigureAwait(false);
                break;
            case FuturesRsiSignalGeneratedFailEvent failed:
                actorContext.Logger.LogError(
                    "{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    failed.EventName, failed.EntityId, failed.ErrorMessage);
                break;
            case FuturesRsiSignalGeneratedCompleteEvent completed:
                await completed.PublishAsync(context).ConfigureAwait(false);
                break;
            case FuturesRsiSignalGeneratedEvent:
            case FuturesRsiSignalsGeneratedEvent:
                break;
            default:
                throw new InvalidOperationException(
                    $"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        }
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) =>
        await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
