using System;
using System.Linq;
using System.Text.Json;
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
    public async Task Borrowed_prepared_quote_parameters_remain_unmodified_after_binding()
    {
        var contractId = "ES-BORROWED-" + Guid.NewGuid().ToString("N");
        var valueDate = new DateOnly(2026, 8, 7);
        var timestamp = new DateTime(2026, 8, 7, 20, 15, 30, DateTimeKind.Utc);
        var quote = new FuturesTickQuoteData(
            1, 2, 3, 0, 5_000_000_000, 5m, 10, 1, 5_100_000_000, 5.1m, 11, 1);
        using var wrapper = new TickQuoteScyllaBindValue(
            new FuturesTickQuoteDataSegment([quote], 1));
        object?[] values =
        [
            (sbyte)AssetTypeId.Futures, contractId, valueDate,
            TimeOnly.FromDateTime(timestamp), 3L, timestamp, timestamp.Ticks,
            (short)1, "GLBX.MDP3", valueDate, 1, 42L,
            Guid.NewGuid(), 0L, Guid.NewGuid(), contractId,
            "integration", timestamp, (short)QuoteEmissionReason.BufferFull,
            (short)1, wrapper
        ];

        await fixture.DevDatabase
            .UseTest(MarketDataDbCql.InsertTickQuoteData)
            .SetParameters((object)values)
            .ExecuteCommandAsync();

        Assert.Same(wrapper, values[20]);
        var rows = await fixture.DevDatabase
            .UseTest("SELECT JSON quote_data FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;")
            .SetParameters((object)new object[] {
                (sbyte)AssetTypeId.Futures, contractId, valueDate,
                TimeOnly.FromDateTime(timestamp), 3L })
            .ExecuteQueryAsync(static row => row.GetString(0));
        using var document = JsonDocument.Parse(Assert.Single(rows));
        AssertQuotesMatch(document.RootElement.GetProperty("quote_data"), [quote]);
    }

    [Fact]
    public async Task Prepared_nested_udt_list_marker_accepts_raw_cql_bytes()
    {
        var contractId = "ES-RAW-LIST-" + Guid.NewGuid().ToString("N");
        var valueDate = new DateOnly(2026, 8, 7);
        var timestamp = new DateTime(2026, 8, 7, 20, 15, 30, DateTimeKind.Utc);
        var payload = new byte[] { 0, 0, 0, 0 }; // Valid CQL binary encoding of an empty list.

        await fixture.DevDatabase
            .UseTest(MarketDataDbCql.InsertTickQuoteData)
            .SetParameters((object)new object?[] {
                (sbyte)AssetTypeId.Futures, contractId, valueDate,
                TimeOnly.FromDateTime(timestamp), 4L, timestamp, timestamp.Ticks,
                (short)1, "GLBX.MDP3", valueDate, 1, 42L,
                Guid.NewGuid(), 0L, Guid.NewGuid(), contractId,
                "integration", timestamp, (short)QuoteEmissionReason.BufferFull,
                (short)0, payload })
            .ExecuteCommandAsync();

        var jsonRows = await fixture.DevDatabase
            .UseTest("SELECT JSON quote_data FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;")
            .SetParameters((object)new object[] {
                (sbyte)AssetTypeId.Futures, contractId, valueDate,
                TimeOnly.FromDateTime(timestamp), 4L })
            .ExecuteQueryAsync(static row => row.GetString(0));
        using var document = JsonDocument.Parse(Assert.Single(jsonRows));
        Assert.Equal(0, document.RootElement.GetProperty("quote_data").GetArrayLength());
    }

    [Theory]
    [InlineData(1, AssetTypeId.Futures)]
    [InlineData(32, AssetTypeId.Futures)]
    [InlineData(64, AssetTypeId.Futures)]
    [InlineData(512, AssetTypeId.Futures)]
    [InlineData(4096, AssetTypeId.Futures)]
    [InlineData(1, AssetTypeId.FuturesOption)]
    [InlineData(32, AssetTypeId.FuturesOption)]
    [InlineData(64, AssetTypeId.FuturesOption)]
    [InlineData(512, AssetTypeId.FuturesOption)]
    [InlineData(4096, AssetTypeId.FuturesOption)]
    public async Task Default_quote_encoder_round_trips_every_udt_field(int count, AssetTypeId assetType)
    {
        var contractId = (assetType == AssetTypeId.Futures ? "ES-" : "ES-OPTION-")
            + "RAW-QUOTES-" + Guid.NewGuid().ToString("N");
        var valueDate = new DateOnly(2026, 8, 7);
        var entity = new TickDataEntityId(contractId, valueDate, assetType);
        var timestamp = new DateTime(2026, 8, 7, 20, 15, 30, DateTimeKind.Utc);
        var quotes = Enumerable.Range(0, count)
            .Select(index => new FuturesTickQuoteData(
                index == count - 1 ? uint.MaxValue : (uint)(index + 1),
                1_000_000_000L + index, 2_000_000_000L + index,
                (byte)(index % 8), 50_000_000_000L + index,
                index == count - 1 ? decimal.MinValue
                    : index % 3 == 0 ? decimal.MaxValue : index % 3 == 1 ? null : -5.25m,
                (uint)(100 + index), (uint)(10 + index),
                51_000_000_000L + index,
                index % 3 == 0 ? 0.0000000000000000000000000001m
                    : index % 3 == 1 ? -5.35m : null,
                (uint)(200 + index), (uint)(20 + index)))
            .ToArray();
        var quote = new FuturesTickQuoteDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickQuoteDataInsertedEvent.Actor,
                FuturesTickQuoteDataInsertedEvent.Verb, entity.Format()),
            Id = Guid.NewGuid(), EntityId = entity, CommandId = Guid.NewGuid(),
            AggregateId = entity.Format(), EventSource = "integration", ReceivedOn = timestamp,
            TickDataId = new TickDataId(contractId, valueDate, 5, timestamp),
            AssetTypeId = assetType, Dataset = "GLBX.MDP3", DefinitionDate = valueDate,
            PublisherId = 1, InstrumentId = 42, EmissionReason = QuoteEmissionReason.BufferFull,
            QuoteCount = (ushort)count,
            QuoteData = new FuturesTickQuoteDataSegment(quotes, (ushort)count)
        };

        await fixture.DevDatabase.InsertTickQuoteDataAsync(quote);

        var jsonRows = await fixture.DevDatabase
            .UseTest("SELECT JSON quote_data FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;")
            .SetParameters((object)new object[] {
                (sbyte)assetType, contractId, valueDate,
                TimeOnly.FromDateTime(timestamp), 5L })
            .ExecuteQueryAsync(static row => row.GetString(0));
        using var document = JsonDocument.Parse(Assert.Single(jsonRows));
        AssertQuotesMatch(document.RootElement.GetProperty("quote_data"), quotes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(64)]
    public async Task Default_quote_writer_preserves_every_udt_field(int count)
    {
        var contractId = "ES-QUOTE-" + Guid.NewGuid().ToString("N");
        var valueDate = new DateOnly(2026, 8, 7);
        var entity = new TickDataEntityId(contractId, valueDate, AssetTypeId.Futures);
        var timestamp = new DateTime(2026, 8, 7, 20, 15, 30, DateTimeKind.Utc);
        var quotes = Enumerable.Range(0, count)
            .Select(index => new FuturesTickQuoteData(
                (uint)(index + 1), 1_000_000_000L + index, 2_000_000_000L + index,
                (byte)(index % 8), 50_000_000_000L + index,
                index % 2 == 0 ? 5.25m + index / 100m : null,
                (uint)(100 + index), (uint)(10 + index),
                51_000_000_000L + index,
                index % 2 == 0 ? null : 5.35m + index / 100m,
                (uint)(200 + index), (uint)(20 + index)))
            .ToArray();
        var quote = new FuturesTickQuoteDataInsertedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesTickQuoteDataInsertedEvent.Actor,
                FuturesTickQuoteDataInsertedEvent.Verb, entity.Format()),
            Id = Guid.NewGuid(), EntityId = entity, CommandId = Guid.NewGuid(),
            AggregateId = entity.Format(), EventSource = "integration", ReceivedOn = timestamp,
            TickDataId = new TickDataId(contractId, valueDate, 3, timestamp),
            AssetTypeId = AssetTypeId.Futures, Dataset = "GLBX.MDP3", DefinitionDate = valueDate,
            PublisherId = 1, InstrumentId = 42, EmissionReason = QuoteEmissionReason.BufferFull,
            QuoteCount = (ushort)count, QuoteData = new FuturesTickQuoteDataSegment(quotes, (ushort)count)
        };

        await fixture.DevDatabase.InsertTickQuoteDataAsync(quote);

        var jsonRows = await fixture.DevDatabase
            .UseTest("SELECT JSON quote_data FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;")
            .SetParameters((object)new object[] {
                (sbyte)AssetTypeId.Futures, contractId, valueDate,
                TimeOnly.FromDateTime(timestamp), 3L })
            .ExecuteQueryAsync(static row => row.GetString(0));
        using var document = JsonDocument.Parse(Assert.Single(jsonRows));
        var stored = document.RootElement.GetProperty("quote_data");
        AssertQuotesMatch(stored, quotes);
    }

    private static void AssertQuotesMatch(JsonElement stored, FuturesTickQuoteData[] quotes)
    {
        Assert.Equal(quotes.Length, stored.GetArrayLength());
        for (var index = 0; index < quotes.Length; index++)
        {
            var expected = quotes[index];
            var actual = stored[index];
            Assert.Equal((long)expected.SourceSequence, actual.GetProperty("source_sequence").GetInt64());
            Assert.Equal(expected.EventTimestampNanoseconds, actual.GetProperty("source_event_timestamp_ns").GetInt64());
            Assert.Equal(expected.ReceiveTimestampNanoseconds, actual.GetProperty("source_receive_timestamp_ns").GetInt64());
            Assert.Equal((short)expected.HeaderFlags, actual.GetProperty("header_flags").GetInt16());
            Assert.Equal(expected.BidPriceRaw, actual.GetProperty("bid_price_raw").GetInt64());
            Assert.Equal(expected.BidPrice, ReadDecimal(actual.GetProperty("bid_price")));
            Assert.Equal((long)expected.BidSize, actual.GetProperty("bid_size").GetInt64());
            Assert.Equal((long)expected.BidCount, actual.GetProperty("bid_count").GetInt64());
            Assert.Equal(expected.AskPriceRaw, actual.GetProperty("ask_price_raw").GetInt64());
            Assert.Equal(expected.AskPrice, ReadDecimal(actual.GetProperty("ask_price")));
            Assert.Equal((long)expected.AskSize, actual.GetProperty("ask_size").GetInt64());
            Assert.Equal((long)expected.AskCount, actual.GetProperty("ask_count").GetInt64());
        }
    }

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

    private static decimal? ReadDecimal(JsonElement value)
        => value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();
}
