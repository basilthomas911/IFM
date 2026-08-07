using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command.State;

public sealed class TickAggregationCommandState
    : BaseEventSourceActorState<TickAggregationCommandState>, IEventSourceActorState<TickAggregationCommandState>
{
    public override ActorThreadId Id { get; set; }

    protected override bool Apply(IEvent domainEvent) => domainEvent switch
    {
        FuturesTickTradeDataInsertedEvent => true,
        FuturesTickQuoteDataInsertedEvent => true,
        _ => false
    };
}
