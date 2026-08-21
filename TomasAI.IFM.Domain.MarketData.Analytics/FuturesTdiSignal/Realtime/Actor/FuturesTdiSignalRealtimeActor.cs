using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;

public class FuturesTdiSignalRealtimeActor(
    IActorSupervisor supervisor,
    IRealtimeProjector<FuturesTdiSignalRealtimeActor> projector,
    ILogger<FuturesTdiSignalRealtimeActor> logger)
    : BaseEventActor<FuturesTdiSignalRealtimeActor>(
        supervisor, logger, new ActorMailboxId(ActorType.Realtime, ActorName))
{
    public const string ActorName = "FuturesTdiSignal";
    readonly FuturesTdiSignalRealtimeState _state = new();
    static readonly ActorTypeId RsiSignalsRoute = new(
        ActorType.Realtime,
        FuturesRsiSignalRealtimeActor.ActorName,
        FuturesRsiSignalsGeneratedEvent.Verb);
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> Parsers = new()
    {
        [FuturesRsiSignalsGeneratedEvent.Verb] = message => message.AsEvent<FuturesRsiSignalsGeneratedEvent>()!,
        [FuturesTdiSignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesTdiSignalGeneratedEvent>()!,
        [FuturesTdiSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesTdiSignalGeneratedCompleteEvent>()!,
        [FuturesTdiSignalGeneratedFailEvent.Verb] = message => message.AsEvent<FuturesTdiSignalGeneratedFailEvent>()!
    };

    protected override async ValueTask OnStartup(IEventActorContext context)
    {
        await projector.StartAsync(context).ConfigureAwait(false);
        context.AddRealtimeRouter(RsiSignalsRoute, Id);
    }

    protected override async ValueTask OnShutdown(IEventActorContext context)
    {
        context.RemoveRealtimeRouter(RsiSignalsRoute, Id);
        await projector.StopAsync().ConfigureAwait(false);
    }

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
        switch (@event)
        {
            case FuturesRsiSignalsGeneratedEvent rsiWindow:
                _ = await rsiWindow.ExecuteRealtimeAsync(projector, _state, logger).ConfigureAwait(false);
                break;
            case FuturesTdiSignalGeneratedFailEvent failed:
                logger.LogError("{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    failed.EventName, failed.EntityId, failed.ErrorMessage);
                break;
            case FuturesTdiSignalGeneratedCompleteEvent completed:
                await completed.PublishAsync(context).ConfigureAwait(false);
                break;
            case FuturesTdiSignalGeneratedEvent:
                break;
            default:
                throw new InvalidOperationException($"Unable to resolve {ActorName} realtime event from message: {@event.Subject}");
        }
    }

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext context, ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
