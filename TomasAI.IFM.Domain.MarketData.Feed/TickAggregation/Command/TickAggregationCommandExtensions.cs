using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command.State;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command;

internal static class TickAggregationCommandExtensions
{
    public static bool Execute(this InsertFuturesTickTradeDataCommand command, TickAggregationCommandState state) =>
        state.Update(new FuturesTickTradeDataInsertedEvent
        {
            Subject = EventSubject(FuturesTickTradeDataInsertedEvent.Verb, command.EntityId),
            Id = command.CommandId, EntityId = command.EntityId, CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(), EventSource = command.EventSource,
            ReceivedOn = command.TickDataId.TimestampUtc,
            SchemaVersion = command.SchemaVersion, TickDataId = command.TickDataId,
            AssetTypeId = command.AssetTypeId, Dataset = command.Dataset,
            DefinitionDate = command.DefinitionDate, PublisherId = command.PublisherId,
            InstrumentId = command.InstrumentId, TradeData = command.TradeData
        }, command);

    public static bool Execute(this InsertFuturesTickQuoteDataCommand command, TickAggregationCommandState state) =>
        state.Update(new FuturesTickQuoteDataInsertedEvent
        {
            Subject = EventSubject(FuturesTickQuoteDataInsertedEvent.Verb, command.EntityId),
            Id = command.CommandId, EntityId = command.EntityId, CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(), EventSource = command.EventSource,
            ReceivedOn = command.TickDataId.TimestampUtc,
            SchemaVersion = command.SchemaVersion, TickDataId = command.TickDataId,
            AssetTypeId = command.AssetTypeId, Dataset = command.Dataset,
            DefinitionDate = command.DefinitionDate, PublisherId = command.PublisherId,
            InstrumentId = command.InstrumentId, EmissionReason = command.EmissionReason,
            QuoteCount = command.QuoteCount, QuoteData = command.QuoteData
        }, command);

    private static ActorSubject EventSubject(string verb, TickDataEntityId entity) =>
        new(ActorType.Event, FuturesTickTradeDataInsertedEvent.Actor, verb, entity.Format());
}
