using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;

/// <summary>Provides the FuturesAdxSignalRealtimeActor implementation.</summary>
public class FuturesAdxSignalRealtimeActor(
    IRealtimeActorContext<FuturesAdxSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesAdxSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesAdxSignalRealtimeContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesAdxSignalRealtimeContext, nameof(actorContext))!;

    public const string ActorName = FuturesAdxSignalSampledRealtimeEvent.Actor;
    readonly FuturesAdxSignalRealtimeState _state = new();
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> Parsers = new()
    {
        [FuturesAdxSignalSampledRealtimeEvent.Verb] = message => message.AsEvent<FuturesAdxSignalSampledRealtimeEvent>()!,
        [FuturesAdxSignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesAdxSignalGeneratedEvent>()!,
        [FuturesAdxSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesAdxSignalGeneratedCompleteEvent>()!,
        [FuturesAdxSignalGeneratedFailEvent.Verb] = message => message.AsEvent<FuturesAdxSignalGeneratedFailEvent>()!
    };

    protected override ValueTask OnStartup(IEventActorContext<FuturesAdxSignalRealtimeActor> context) => actorContext.Projector.StartAsync(context);
    protected override ValueTask OnShutdown(IEventActorContext<FuturesAdxSignalRealtimeActor> context) => actorContext.Projector.StopAsync();

    protected override IEvent ParseMessage(IEventActorContext<FuturesAdxSignalRealtimeActor> context, IActorMessage message)
    {
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName }
            || !Parsers.TryGetValue(subject.Verb, out var parser))
            return default!;
        var @event = parser(message);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesAdxSignalRealtimeActor> context, IEvent @event)
    {
        var dispatchContext = context;
        switch (@event)
        {
            case FuturesAdxSignalSampledRealtimeEvent sampled:
                _ = await sampled.ExecuteAsync(actorContext.Projector, _state, actorContext.Logger).ConfigureAwait(false);
                break;
            case FuturesAdxSignalGeneratedFailEvent failed:
                actorContext.Logger.LogError("{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
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
        IEventActorContext<FuturesAdxSignalRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
