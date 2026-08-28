using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
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
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesTradeSessionBarClosedRealtimeEvent.Verb] =
                message => message.AsEvent<FuturesTradeSessionBarClosedRealtimeEvent>()!
        };
    readonly IFuturesEmaSignalRealtimeContext typedContext = IsArgumentNull.Set(
        actorContext as IFuturesEmaSignalRealtimeContext, nameof(actorContext))!;
    readonly IReadOnlyDictionary<Type, Func<IEvent, IFuturesEmaSignalRealtimeContext, ValueTask<bool>>> _receiveMap =
        new Dictionary<Type, Func<IEvent, IFuturesEmaSignalRealtimeContext, ValueTask<bool>>>
        {
            [typeof(FuturesTradeSessionBarClosedRealtimeEvent)] = (@event, context) =>
                ((FuturesTradeSessionBarClosedRealtimeEvent)@event).ExecuteAsync(context, context.Logger)
        };

    /// <inheritdoc />
    protected override ValueTask OnStartup(IEventActorContext<FuturesEmaSignalRealtimeActor> context)
    { context.AddRealtimeRouter(Route, Id); return ValueTask.CompletedTask; }
    /// <inheritdoc />
    protected override ValueTask OnShutdown(IEventActorContext<FuturesEmaSignalRealtimeActor> context)
    { context.RemoveRealtimeRouter(Route, Id); return ValueTask.CompletedTask; }
    /// <inheritdoc />
    protected override IEvent ParseMessage(IEventActorContext<FuturesEmaSignalRealtimeActor> context, IActorMessage message) =>
        ParseMappedRealtimeEvent(context, message, _parseMap);
    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(IEventActorContext<FuturesEmaSignalRealtimeActor> context, IEvent @event)
    {
        ArgumentNullException.ThrowIfNull(context);
        var handler = ResolveMappedEventHandler(@event, _receiveMap);
        _ = await handler(@event, typedContext).ConfigureAwait(false);
    }
    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(IEventActorContext<FuturesEmaSignalRealtimeActor> context,
        ActorThreadId threadId, IEvent @event, Exception exception) =>
        await exception.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context);
}
