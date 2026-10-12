using Microsoft.Extensions.Logging;
using System.Collections.Frozen;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
public sealed class IronCondorTradePositionRealtimeActor(IRealtimeActorContext<IronCondorTradePositionRealtimeActor> actorContext) : BaseEventActor<IronCondorTradePositionRealtimeActor>(actorContext, Require(actorContext).Logger)
{
    public const string ActorName = "IronCondorTradePositionRealtime";
    readonly IIronCondorTradePositionRealtimeContext _context = Require(actorContext);
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [PositionChangedEvent.Verb] = static message => message.AsEvent<IronCondorPositionChangedEvent>()!,
        [IronCondorMonitoringInputsChangedEvent.Verb] = static message => message.AsEvent<IronCondorMonitoringInputsChangedEvent>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<IEvent, IIronCondorTradePositionRealtimeContext, ValueTask>> _receiveMap = new Dictionary<Type, Func<IEvent, IIronCondorTradePositionRealtimeContext, ValueTask>>
    {
        [typeof(IronCondorPositionChangedEvent)] = static (@event, context) => ((IronCondorPositionChangedEvent)@event).ExecuteAsync(context),
        [typeof(IronCondorMonitoringInputsChangedEvent)] = static (@event, context) => ((IronCondorMonitoringInputsChangedEvent)@event).ExecuteAsync(context)
    }.ToFrozenDictionary();
    protected override IEvent ParseMessage(IEventActorContext<IronCondorTradePositionRealtimeActor> context, IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);
    protected override ValueTask ReceiveAsync(IEventActorContext<IronCondorTradePositionRealtimeActor> context, IEvent @event) => ResolveMappedEventHandler(@event, _receiveMap)(@event, _context);
    protected override ValueTask OnExceptionAsync(IEventActorContext<IronCondorTradePositionRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (_context.Logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Error))
            global::TomasAI.IFM.Domain.Trade.TradeBoundaryDiagnosticLog.Write4(_context.Logger, ex, nameof(IronCondorTradePositionRealtimeActor), nameof(OnExceptionAsync), @event.AggregateId);
        return ValueTask.CompletedTask;
    }

    static IIronCondorTradePositionRealtimeContext Require(IRealtimeActorContext<IronCondorTradePositionRealtimeActor> context) => context as IIronCondorTradePositionRealtimeContext ?? throw new ArgumentException("Typed Iron Condor position realtime context required.", nameof(context));
}
