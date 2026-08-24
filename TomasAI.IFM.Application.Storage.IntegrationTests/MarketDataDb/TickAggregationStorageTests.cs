using System;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class TickAggregationStorageTests(MarketDataFixture fixture) : IClassFixture<MarketDataFixture>
{
    [Fact]
    public async Task Trade_and_bounded_quote_array_round_trip_through_authoritative_tables()
    {
        var contractId = "ES-TEST-" + Guid.NewGuid().ToString("N");
        var valueDate = new DateOnly(2026, 8, 7);
        var entity = new TickDataEntityId(contractId, valueDate, AssetTypeId.Futures);
        var timestamp = new DateTime(2026, 8, 7, 20, 15, 30, DateTimeKind.Utc);
        var quotes = new[]
        {
            new FuturesTickQuoteData(1, 2, 3, 0, 5_000_000_000, 5m, 10, 1, 5_100_000_000, 5.1m, 11, 1),
            new FuturesTickQuoteData(2, 4, 5, 0, 5_010_000_000, 5.01m, 12, 1, 5_110_000_000, 5.11m, 13, 1)
        };
        var trade = BaseTrade(entity, timestamp);
        var quote = new FuturesTickQuoteDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickQuoteDataInsertedEvent.Actor,
                FuturesTickQuoteDataInsertedEvent.Verb, entity.Format()),
            Id = Guid.NewGuid(), EntityId = entity, CommandId = Guid.NewGuid(),
            AggregateId = entity.Format(), EventSource = "integration", ReceivedOn = timestamp,
            TickDataId = new TickDataId(contractId, valueDate, 2, timestamp.AddTicks(1)),
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", DefinitionDate = valueDate,
            PublisherId = 1, InstrumentId = 42, EmissionReason = QuoteEmissionReason.TradeObserved,
            QuoteCount = 2, QuoteData = new FuturesTickQuoteDataSegment(quotes, 2)
        };

        await fixture.DevDatabase.InsertTickTradeDataAsync(trade);
        await fixture.DevDatabase.InsertTickQuoteDataAsync(quote);

        var tradeRows = await fixture.DevDatabase
            .UseTest("SELECT sequence_id FROM tick_trade_data WHERE asset_type_id=? AND contract_id=? AND value_date>=? AND value_date<=?;")
            .SetParameters((object)new object[] { (sbyte)AssetTypeId.Futures, contractId, valueDate, valueDate })
            .ExecuteQueryImmutableAsync(static row => row.GetLong(0));
        var quoteRows = await fixture.DevDatabase
            .UseTest("SELECT sequence_id, quote_count FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date>=? AND value_date<=?;")
            .SetParameters((object)new object[] { (sbyte)AssetTypeId.Futures, contractId, valueDate, valueDate })
            .ExecuteQueryImmutableAsync(static row => new QuoteResult(row.GetLong(0), row.GetShort(1)));
        var startTime = TimeOnly.FromDateTime(timestamp.AddSeconds(-1));
        var endTime = TimeOnly.FromDateTime(timestamp.AddSeconds(1));
        var intradayTrades = await fixture.DevDatabase
            .UseTest("SELECT sequence_id FROM tick_trade_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time>=? AND aggregation_time<=?;")
            .SetParameters((object)new object[] {
                (sbyte)AssetTypeId.Futures, contractId, valueDate, startTime, endTime })
            .ExecuteQueryImmutableAsync(static row => row.GetLong(0));
        var exactQuotes = await fixture.DevDatabase
            .UseTest("SELECT sequence_id FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND (value_date, aggregation_time)>=(?, ?) AND (value_date, aggregation_time)<=(?, ?);")
            .SetParameters((object)new object[] {
                (sbyte)AssetTypeId.Futures, contractId,
                valueDate, startTime, valueDate, endTime })
            .ExecuteQueryImmutableAsync(static row => row.GetLong(0));

        Assert.Contains(1L, tradeRows);
        Assert.Contains(quoteRows, row => row.SequenceId == 2 && row.QuoteCount == 2);
        Assert.Contains(1L, intradayTrades);
        Assert.Contains(2L, exactQuotes);
    }

    private static FuturesTickTradeDataInsertedEvent BaseTrade(TickDataEntityId entity, DateTime timestamp) => new()
    {
        Subject = new ActorSubject(ActorType.Event, FuturesTickTradeDataInsertedEvent.Actor,
            FuturesTickTradeDataInsertedEvent.Verb, entity.Format()),
        Id = Guid.NewGuid(), EntityId = entity, CommandId = Guid.NewGuid(), AggregateId = entity.Format(),
        EventSource = "integration", ReceivedOn = timestamp,
        TickDataId = new TickDataId(entity.ContractId, entity.ValueDate, 1, timestamp),
        AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", DefinitionDate = entity.ValueDate,
        PublisherId = 1, InstrumentId = 42,
        TradeData = new FuturesTickTradeData(1, 2, 3, 0, 5_050_000_000, 5.05m, 10, 1, 2, 0)
    };

    private readonly record struct QuoteResult(long SequenceId, short QuoteCount);
}
