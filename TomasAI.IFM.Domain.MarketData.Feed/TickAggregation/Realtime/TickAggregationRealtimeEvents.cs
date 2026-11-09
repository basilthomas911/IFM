using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Realtime;

internal static class TickAggregationRealtimeEvents
{

    extension(FuturesTickTradeDataChangedEvent value)
    {
        internal FuturesTickTradeDataChangedEvent RestoreSource(
        string? dataset, Guid generationId) => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with
        {
            SourceDataset = dataset ?? string.Empty,
            SourceGenerationId = generationId
        });
    }

    extension(FuturesTickQuoteDataChangedEvent value)
    {
        internal FuturesTickQuoteDataChangedEvent RestoreSource(
        string? dataset, Guid generationId) => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(value, value with
        {
            SourceDataset = dataset ?? string.Empty,
            SourceGenerationId = generationId
        });
    }

    extension(FuturesTickTradeDataChangedEvent source)
    {
        internal FuturesTickTradeDataInsertedEvent ToInsertedEvent(
        ) => new()
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
            TradeData = source.TradeData,
            SourceDataset = source.SourceDataset,
            SourceGenerationId = source.SourceGenerationId
        };
    }

    extension(FuturesTickQuoteDataChangedEvent source)
    {
        internal FuturesTickQuoteDataInsertedEvent ToInsertedEvent(
        ) => new()
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
            QuoteData = source.QuoteData,
            SourceDataset = source.SourceDataset,
            SourceGenerationId = source.SourceGenerationId
        };
    }
}
