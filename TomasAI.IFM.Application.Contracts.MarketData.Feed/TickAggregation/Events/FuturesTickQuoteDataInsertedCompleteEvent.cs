using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;

[MessagePackObject]
public record FuturesTickQuoteDataInsertedCompleteEvent : TickAggregationCompleteEvent
{
    public new const string Verb = "FuturesTickQuoteDataInsertedComplete";
}
