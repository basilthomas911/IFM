using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;

public class FuturesAdxSignalRealtimeActor(
    IActorSupervisor supervisor,
    IRealtimeProjector<FuturesAdxSignalRealtimeActor> projector,
    ILogger<FuturesAdxSignalRealtimeActor> logger)
    : BaseEventActor<FuturesAdxSignalRealtimeActor>(
        supervisor, logger, new ActorMailboxId(ActorType.Realtime, ActorName))
{
    public const string ActorName = FuturesAdxSignalSampledRealtimeEvent.Actor;
    readonly FuturesAdxSignalRealtimeState _state = new();
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> Parsers = new()
    {
        [FuturesAdxSignalSampledRealtimeEvent.Verb] = message => message.AsEvent<FuturesAdxSignalSampledRealtimeEvent>()!,
        [FuturesAdxSignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesAdxSignalGeneratedEvent>()!,
        [FuturesAdxSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesAdxSignalGeneratedCompleteEvent>()!,
        [FuturesAdxSignalGeneratedFailEvent.Verb] = message => message.AsEvent<FuturesAdxSignalGeneratedFailEvent>()!
    };

    protected override ValueTask OnStartup(IEventActorContext context) => projector.StartAsync(context);
    protected override ValueTask OnShutdown(IEventActorContext context) => projector.StopAsync();

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
            case FuturesAdxSignalSampledRealtimeEvent sampled:
                _ = await sampled.ExecuteAsync(projector, _state, logger).ConfigureAwait(false);
                break;
            case FuturesAdxSignalGeneratedFailEvent failed:
                logger.LogError("{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    failed.EventName, failed.EntityId, failed.ErrorMessage);
                break;
            case FuturesAdxSignalGeneratedEvent:
            case FuturesAdxSignalGeneratedCompleteEvent:
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
