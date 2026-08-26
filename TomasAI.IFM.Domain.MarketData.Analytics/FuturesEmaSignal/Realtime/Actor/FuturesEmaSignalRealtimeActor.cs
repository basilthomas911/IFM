using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime.Actor;

/// <summary>Statelessly routes shared closed bars to EMA command processing.</summary>
public sealed class FuturesEmaSignalRealtimeActor(IRealtimeActorContext<FuturesEmaSignalRealtimeActor> actorContext)
    : BaseEventActor<FuturesEmaSignalRealtimeActor>(actorContext,
        ((IFuturesEmaSignalRealtimeContext)actorContext).Logger)
{
    /// <summary>Gets the realtime mailbox name.</summary>
    public const string ActorName = "FuturesEmaSignal";
    static readonly ActorTypeId Route = new(ActorType.Realtime,
        FuturesTradeSessionBarClosedRealtimeEvent.Actor, FuturesTradeSessionBarClosedRealtimeEvent.Verb);
    readonly IFuturesEmaSignalRealtimeContext typedContext = IsArgumentNull.Set(
        actorContext as IFuturesEmaSignalRealtimeContext, nameof(actorContext))!;

    /// <inheritdoc />
    protected override ValueTask OnStartup(IEventActorContext<FuturesEmaSignalRealtimeActor> context)
    { context.AddRealtimeRouter(Route, Id); return ValueTask.CompletedTask; }
    /// <inheritdoc />
    protected override ValueTask OnShutdown(IEventActorContext<FuturesEmaSignalRealtimeActor> context)
    { context.RemoveRealtimeRouter(Route, Id); return ValueTask.CompletedTask; }
    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesEmaSignalRealtimeActor> context, IActorMessage message) =>
        message.Subject.Is(ActorType.Realtime, ActorName, FuturesTradeSessionBarClosedRealtimeEvent.Verb)
            ? message.AsEvent<FuturesTradeSessionBarClosedRealtimeEvent>()! : default!;
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesEmaSignalRealtimeActor> context, IEvent @event)
    {
        if (@event is not FuturesTradeSessionBarClosedRealtimeEvent closed)
            throw new InvalidOperationException($"Unsupported EMA realtime event {@event.EventName}.");
        _ = await closed.ExecuteAsync(typedContext, typedContext.Logger);
    }
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesEmaSignalRealtimeActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
