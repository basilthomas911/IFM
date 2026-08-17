using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;

internal static class TickAggregationRealtimeEvents
{
    internal static FuturesTickTradeDataInsertedEvent ToInsertedEvent(
        this FuturesTickTradeDataChangedEvent source) => new()
    {
        Subject = new ActorSubject(
            ActorType.Realtime,
            FuturesTickTradeDataInsertedEvent.Actor,
            FuturesTickTradeDataInsertedEvent.Verb,
            source.EntityId.Format()),
        Id = source.Id,
        EntityId = source.EntityId,
        EventId = 0,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = source.EventSource,
        ReceivedOn = source.ReceivedOn,
        SchemaVersion = source.SchemaVersion,
        TickDataId = source.TickDataId,
        AssetTypeId = source.AssetTypeId,
        Dataset = source.Dataset,
        DefinitionDate = source.DefinitionDate,
        PublisherId = source.PublisherId,
        InstrumentId = source.InstrumentId,
        TradeData = source.TradeData
    };

    internal static FuturesTickQuoteDataInsertedEvent ToInsertedEvent(
        this FuturesTickQuoteDataChangedEvent source) => new()
    {
        Subject = new ActorSubject(
            ActorType.Realtime,
            FuturesTickQuoteDataInsertedEvent.Actor,
            FuturesTickQuoteDataInsertedEvent.Verb,
            source.EntityId.Format()),
        Id = source.Id,
        EntityId = source.EntityId,
        EventId = 0,
        CommandId = source.CommandId,
        AggregateId = source.AggregateId,
        EventSource = source.EventSource,
        ReceivedOn = source.ReceivedOn,
        SchemaVersion = source.SchemaVersion,
        TickDataId = source.TickDataId,
        AssetTypeId = source.AssetTypeId,
        Dataset = source.Dataset,
        DefinitionDate = source.DefinitionDate,
        PublisherId = source.PublisherId,
        InstrumentId = source.InstrumentId,
        EmissionReason = source.EmissionReason,
        QuoteCount = source.QuoteCount,
        QuoteData = source.QuoteData
    };
}
