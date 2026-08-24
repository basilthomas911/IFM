using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Extensions;

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
    readonly FuturesMacdSignalRealtimeState _state = new();
    static readonly Dictionary<string, Func<IActorMessage, IEvent>> Parsers = new()
    {
        [FuturesMacdSignalSampledRealtimeEvent.Verb] = message => message.AsEvent<FuturesMacdSignalSampledRealtimeEvent>()!,
        [FuturesMacdSignalGeneratedEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedEvent>()!,
        [FuturesMacdSignalGeneratedCompleteEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedCompleteEvent>()!,
        [FuturesMacdSignalGeneratedFailEvent.Verb] = message => message.AsEvent<FuturesMacdSignalGeneratedFailEvent>()!
    };

    protected override ValueTask OnStartup(IEventActorContext<FuturesMacdSignalRealtimeActor> context) => actorContext.Projector.StartAsync(context);
    protected override ValueTask OnShutdown(IEventActorContext<FuturesMacdSignalRealtimeActor> context) => actorContext.Projector.StopAsync();

    protected override IEvent ParseMessage(IEventActorContext<FuturesMacdSignalRealtimeActor> context, IActorMessage message)
    {
        var subject = message.Subject;
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

    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesMacdSignalRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
