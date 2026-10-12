using System.Collections.Frozen;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;
/// <summary>Routes provider-neutral quote changes to working emulator broker orders.</summary>
public sealed class BrokerOrderRealtimeActor(IRealtimeActorContext<BrokerOrderRealtimeActor> context) : BaseEventActor<BrokerOrderRealtimeActor>(context, Typed(context).Logger)
{
    public const string ActorName = "BrokerOrderRealtime";
    private static readonly ActorTypeId QuoteRoute = new(ActorType.Realtime, FuturesTickQuoteDataChangedEvent.Actor, FuturesTickQuoteDataChangedEvent.Verb);
    private static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap = new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
    {
        [FuturesTickQuoteDataChangedEvent.Verb] = message => message.AsEvent<FuturesTickQuoteDataChangedEvent>()!
    }.ToFrozenDictionary(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<Type, Func<IBrokerOrderRealtimeContext, IEvent, ValueTask>> _receiveMap = new Dictionary<Type, Func<IBrokerOrderRealtimeContext, IEvent, ValueTask>>
    {
        [typeof(FuturesTickQuoteDataChangedEvent)] = static (runtime, domainEvent) => ((FuturesTickQuoteDataChangedEvent)domainEvent).ExecuteAsync(runtime)
    }.ToFrozenDictionary();
    private readonly IBrokerOrderRealtimeContext _runtime = Typed(context);
    /// <inheritdoc/>
    protected override ValueTask OnStartup(IEventActorContext<BrokerOrderRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.AddRealtimeRouter(QuoteRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override ValueTask OnShutdown(IEventActorContext<BrokerOrderRealtimeActor> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RemoveRealtimeRouter(QuoteRoute, Id);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    protected override IEvent ParseMessage(IEventActorContext<BrokerOrderRealtimeActor> context, IActorMessage message) => ParseMappedRealtimeEvent(context, message, _parseMap);
    /// <inheritdoc/>
    protected override ValueTask ReceiveAsync(IEventActorContext<BrokerOrderRealtimeActor> context, IEvent @event) => ResolveMappedEventHandler(@event, _receiveMap)(_runtime, @event);
    /// <inheritdoc/>
    protected override async ValueTask OnExceptionAsync(IEventActorContext<BrokerOrderRealtimeActor> context, ActorThreadId threadId, IEvent @event, Exception ex) => await ex.SendErrorEventAsync<TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent, ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
    private static IBrokerOrderRealtimeContext Typed(IRealtimeActorContext<BrokerOrderRealtimeActor> context) => context as IBrokerOrderRealtimeContext ?? throw new ArgumentException("Typed BrokerOrder realtime context required.", nameof(context));
}
