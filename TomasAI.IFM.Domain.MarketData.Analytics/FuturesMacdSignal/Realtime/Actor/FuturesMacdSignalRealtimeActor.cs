using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;

public class FuturesMacdSignalRealtimeActor(
    IActorSupervisor supervisor,
    IRealtimeProjector<FuturesMacdSignalRealtimeActor> projector,
    ILogger<FuturesMacdSignalRealtimeActor> logger)
    : BaseEventActor<FuturesMacdSignalRealtimeActor>(
        supervisor, logger, new ActorMailboxId(ActorType.Realtime, ActorName))
{
    public const string ActorName = FuturesMacdSignalSampledRealtimeEvent.Actor;
    readonly FuturesMacdSignalRealtimeState _state = new();
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> Parsers = new()
    {
        [FuturesMacdSignalSampledRealtimeEvent.Verb] = message => message.AsEvent<FuturesMacdSignalSampledRealtimeEvent>()!,
        [FuturesMacdSignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedEvent>()!,
        [FuturesMacdSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedCompleteEvent>()!,
        [FuturesMacdSignalGeneratedFailEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedFailEvent>()!
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
            case FuturesMacdSignalSampledRealtimeEvent sampled:
                _ = await sampled.ExecuteAsync(projector, _state, logger).ConfigureAwait(false);
                break;
            case FuturesMacdSignalGeneratedFailEvent failed:
                logger.LogError("{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
                    failed.EventName, failed.EntityId, failed.ErrorMessage);
                break;
            case FuturesMacdSignalGeneratedEvent:
            case FuturesMacdSignalGeneratedCompleteEvent:
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
