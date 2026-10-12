using Microsoft.Extensions.Logging;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor;
public sealed class FuturesTradePositionRealtimeActor(IRealtimeActorContext<FuturesTradePositionRealtimeActor> actorContext) : BaseEventActor<FuturesTradePositionRealtimeActor>(actorContext, Require(actorContext).Logger)
{
    public const string ActorName = "FuturesTradePositionRealtime";
    readonly IFuturesTradePositionRealtimeContext _context = Require(actorContext);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [PositionChangedEvent.Verb] = static message => message.AsEvent<FuturesPositionChangedEvent>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<FuturesPositionChangedEvent, IFuturesTradePositionRealtimeContext, ValueTask>> _receiveMap = new Dictionary<Type, Func<FuturesPositionChangedEvent, IFuturesTradePositionRealtimeContext, ValueTask>>
    {
        [typeof(FuturesPositionChangedEvent)] = static (@event, context) => @event.ExecuteAsync(context)
    }.ToFrozenDictionary();
    protected override IEvent ParseMessage(IEventActorContext<FuturesTradePositionRealtimeActor> context, IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IEventActorContext<FuturesTradePositionRealtimeActor> context, IEvent @event) => ResolveMappedEventHandler(@event, _receiveMap)((FuturesPositionChangedEvent)@event, _context);
    protected override ValueTask OnExceptionAsync(IEventActorContext<FuturesTradePositionRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (_context.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
            global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write2(_context.Logger, ex, nameof(FuturesTradePositionRealtimeActor), nameof(OnExceptionAsync), @event.AggregateId);
        return ValueTask.CompletedTask;
    }

    static IFuturesTradePositionRealtimeContext Require(IRealtimeActorContext<FuturesTradePositionRealtimeActor> context) => context as IFuturesTradePositionRealtimeContext ?? throw new ArgumentException("Typed Futures position realtime context required.", nameof(context));
}
