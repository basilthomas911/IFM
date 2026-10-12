using Microsoft.Extensions.Logging;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.VerticalSpread.Realtime.Actor;
public sealed class VerticalSpreadTradePositionRealtimeActor(IRealtimeActorContext<VerticalSpreadTradePositionRealtimeActor> actorContext) : BaseEventActor<VerticalSpreadTradePositionRealtimeActor>(actorContext, Require(actorContext).Logger)
{
    public const string ActorName = "VerticalSpreadTradePositionRealtime";
    readonly IVerticalSpreadTradePositionRealtimeContext _context = Require(actorContext);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [PositionChangedEvent.Verb] = static message => message.AsEvent<VerticalSpreadPositionChangedEvent>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<VerticalSpreadPositionChangedEvent, IVerticalSpreadTradePositionRealtimeContext, ValueTask>> _receiveMap = new Dictionary<Type, Func<VerticalSpreadPositionChangedEvent, IVerticalSpreadTradePositionRealtimeContext, ValueTask>>
    {
        [typeof(VerticalSpreadPositionChangedEvent)] = static (@event, context) => @event.ExecuteAsync(context)
    }.ToFrozenDictionary();
    protected override IEvent ParseMessage(IEventActorContext<VerticalSpreadTradePositionRealtimeActor> context, IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IEventActorContext<VerticalSpreadTradePositionRealtimeActor> context, IEvent @event) => ResolveMappedEventHandler(@event, _receiveMap)((VerticalSpreadPositionChangedEvent)@event, _context);
    protected override ValueTask OnExceptionAsync(IEventActorContext<VerticalSpreadTradePositionRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (_context.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
            global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write5(_context.Logger, ex, nameof(VerticalSpreadTradePositionRealtimeActor), nameof(OnExceptionAsync), @event.AggregateId);
        return ValueTask.CompletedTask;
    }

    static IVerticalSpreadTradePositionRealtimeContext Require(IRealtimeActorContext<VerticalSpreadTradePositionRealtimeActor> context) => context as IVerticalSpreadTradePositionRealtimeContext ?? throw new ArgumentException("Typed Vertical Spread position realtime context required.", nameof(context));
}
