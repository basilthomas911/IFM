using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketEvaluationSnapshot;

using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;

/// <summary>Provides the FuturesTdiSignalRealtimeActor implementation.</summary>
public class FuturesTdiSignalRealtimeActor(
    IRealtimeActorContext<FuturesTdiSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesTdiSignalRealtimeActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the domain-specific typed context owned by this actor.</summary>
    protected IFuturesTdiSignalRealtimeContext ActorContext { get; } =
        IsArgumentNull.Set(actorContext as IFuturesTdiSignalRealtimeContext, nameof(actorContext))!;

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

    protected override async ValueTask OnStartup(IEventActorContext<FuturesTdiSignalRealtimeActor> context)
    {
        await actorContext.Projector.StartAsync(context).ConfigureAwait(false);
        context.AddRealtimeRouter(RsiSignalsRoute, Id);
    }

    protected override async ValueTask OnShutdown(IEventActorContext<FuturesTdiSignalRealtimeActor> context)
    {
        context.RemoveRealtimeRouter(RsiSignalsRoute, Id);
        await actorContext.Projector.StopAsync().ConfigureAwait(false);
    }

    protected override IEvent ParseMessage(IEventActorContext<FuturesTdiSignalRealtimeActor> context, IActorMessage message)
    {
        var subject = message.Subject;
        if (subject is not { ActorType: ActorType.Realtime, Name: ActorName }
            || !Parsers.TryGetValue(subject.Verb, out var parser))
            return default!;
        var @event = parser(message);
        @event.CheckForEmptyCommandId();
        return @event;
    }

    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesTdiSignalRealtimeActor> context, IEvent @event)
    {
        var dispatchContext = context;
        switch (@event)
        {
            case FuturesRsiSignalsGeneratedEvent rsiWindow:
                _ = await rsiWindow.ExecuteRealtimeAsync(actorContext.Projector, _state, actorContext.Logger).ConfigureAwait(false);
                break;
            case FuturesTdiSignalGeneratedFailEvent failed:
                actorContext.Logger.LogError("{EventName} for {EntityId}: {ErrorMessage}; no replay or retry will be attempted",
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
        IEventActorContext<FuturesTdiSignalRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
