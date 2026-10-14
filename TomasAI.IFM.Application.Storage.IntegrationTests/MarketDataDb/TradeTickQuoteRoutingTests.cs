using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Verifies raw ownership with separate PostgreSQL and isolated CQL destinations.</summary>
public sealed class TradeTickQuoteRoutingTests
{
    [Fact]
    public async Task Native_market_data_routes_raw_ticks_to_scylla_and_keeps_both_indexes_consistent()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("raw-tick-routing");
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings()
            .Add("MarketDataDbConnection", infrastructure.PostgresConnectionString, "System.Data.Postgres")
            .Add("TradeTickQuoteDbConnection", infrastructure.CqlConnectionString, "System.Data.ScyllaDb");
        var logger = Substitute.For<ILogger<DbProvider>>();
        await new TradeTickQuoteSchemaDb(settings, logger).CreateAllAsync();
        var raw = new TradeTickQuoteDbContext(settings, logger);
        var repositories = new Dictionary<Type, IObjectRepository>
        {
            [typeof(IObjectRepository<TradeTickQuoteDbContext>)] = raw
        };
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var board = new BlackboardService(Substitute.For<IRedisCache>(), new SystemTextJsonSerializer());
        var market = new Storage.MarketDataDb.MarketDataDbContext(settings, factory, board,
            Substitute.For<ISequenceIdGenerator>(), logger);
        // No MarketData schema or factory repository exists: any accidental raw PostgreSQL access fails.
        var date = new DateOnly(2026, 10, 7);
        var contract = "ES-RAW-" + infrastructure.RunId;
        var ticks = Enumerable.Range(1, 55).Select(i => SampleData.FuturesTickData with
        {
            ContractId = contract, ValueDate = date, TickId = i,
            TickTime = new TimeOnly(12, 0).Add(TimeSpan.FromSeconds(i)), Price = 6000m + i / 4m
        }).ToArray();
        await market.InsertFuturesTickDataAsync(ticks);
        Assert.Equal(55, (await market.GetLastFuturesTickDataIdAsync(contract, date))!.TickId);
        Assert.Equal(ticks[0], await market.GetFuturesTickDataAsync(new TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesTickDataId(contract, date, ticks[0].TickId)));
        Assert.Equal(30, (await market.GetFuturesTickAtOrBeforeAsync(contract, date, new TimeOnly(12, 0, 30)))!.TickId);
        Assert.Null(await market.GetFuturesTickAtOrBeforeAsync(contract, date.AddDays(1), new TimeOnly(12, 1)));
        Assert.Null(await market.GetFuturesTickAtOrBeforeAsync(contract, date, new TimeOnly(11, 59)));
        Assert.Equal(30, (await market.GetLastFuturesTickDataByTickDateAsync(contract, date.ToDateTime(new TimeOnly(12, 0, 30))))!.TickId);
        await Assert.ThrowsAsync<ArgumentException>(() => market.InsertFuturesTickDataAsync(new[] { ticks[0], ticks[0] }));
        await market.DeleteFuturesTickDataAsync(contract, date);
        Assert.Null(await market.GetLastFuturesTickDataAsync(contract, date));
        Assert.Null(await market.GetFuturesTickAtOrBeforeAsync(contract, date, new TimeOnly(13, 0)));

        var option = SampleData.FuturesOptionTickData with
        {
            ContractId = contract + "-P", ValueDate = date, TickId = 42,
            OptionPrice = 12.125, BidPrice = 12.0, AskPrice = 12.25,
            ImpliedVolatility = 0.213456, Delta = -0.161234, Gamma = 0.002345
        };
        await market.InsertFuturesOptionTickDataAsync(option);
        var saved = await market.GetFuturesOptionTickDataAsync(new TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesOptionTickDataId(option.ContractId, date, option.TickId));
        Assert.NotNull(saved);
        Assert.Equal(option.OptionPrice, saved.OptionPrice);
        Assert.Equal(option.Delta, saved.Delta);
        Assert.Equal(option.Gamma, saved.Gamma);
        Assert.Equal(42, (await market.GetLastFuturesOptionTickDataIdAsync(option.ContractId, date))!.TickId);
        await market.DeleteFuturesOptionTickDataAsync(option.ContractId, date);
        Assert.Null(await market.GetLastFuturesOptionTickDataAsync(option.ContractId, date));

        // Verify direct ingestion through the new owner using this same disposable fixture.
        var timestamp = new DateTime(2026, 10, 7, 16, 1, 2, DateTimeKind.Utc).AddTicks(1234);
        var trade = new FuturesTickTradeDataInsertedEvent
        {
            Id = Guid.NewGuid(), EventId = 81, CommandId = Guid.NewGuid(),
            AggregateId = contract, EventSource = "isolated-integration", ReceivedOn = timestamp,
            TickDataId = new(contract, date, 1234, timestamp), AssetTypeId = AssetTypeId.Futures,
            Dataset = "GLBX.MDP3", DefinitionDate = date, PublisherId = ushort.MaxValue, InstrumentId = uint.MaxValue,
            TradeData = new(uint.MaxValue, 1234567890123456789, 1234567890123456799, byte.MaxValue, -5250000000, -5.25m, uint.MaxValue, 1, 2, 3)
        };
        await market.InsertTickTradeDataAsync(trade);
        var tradeJson = await raw.UseTest("SELECT JSON * FROM tick_trade_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;")
            .SetParameters((object)new object[] { (sbyte)AssetTypeId.Futures, contract, date, TimeOnly.FromDateTime(timestamp), 1234L })
            .ExecuteQueryAsync(row => row.GetString(0));
        using (var document = JsonDocument.Parse(Assert.Single(tradeJson)))
        {
            var row = document.RootElement;
            Assert.Equal(trade.Id, row.GetProperty("actor_event_id").GetGuid());
            Assert.Equal(trade.EventId, row.GetProperty("actor_event_log_id").GetInt64());
            Assert.Equal(timestamp.Ticks, row.GetProperty("aggregation_timestamp_utc_ticks").GetInt64());
            Assert.Equal((long)uint.MaxValue, row.GetProperty("instrument_id").GetInt64());
            Assert.Equal((long)uint.MaxValue, row.GetProperty("source_sequence").GetInt64());
            Assert.Equal(trade.TradeData.EventTimestampNanoseconds, row.GetProperty("source_event_timestamp_ns").GetInt64());
            Assert.Equal(trade.TradeData.ReceiveTimestampNanoseconds, row.GetProperty("source_receive_timestamp_ns").GetInt64());
            Assert.Equal(trade.TradeData.Price, row.GetProperty("price").GetDecimal());
            Assert.Equal(trade.TradeData.PriceRaw, row.GetProperty("price_raw").GetInt64());
            Assert.Equal((long)uint.MaxValue, row.GetProperty("size").GetInt64());
        }
        foreach (var count in new[] { 1, 512 })
        {
            var quotes = Enumerable.Range(0, count).Select(index => new FuturesTickQuoteData(
                index == count - 1 ? uint.MaxValue : (uint)index, 1234567890123456789 + index, 1234567890123456799 + index,
                (byte)(index % 8), 6000125000000 + index, index % 2 == 0 ? 6000.125m + index : null,
                (uint)(100 + index), (uint)(10 + index), 6000250000000 + index, index % 2 == 0 ? null : 6000.25m + index,
                (uint)(200 + index), (uint)(20 + index))).ToArray();
            var quote = new FuturesTickQuoteDataInsertedEvent
            {
                Id = Guid.NewGuid(), EventId = 82, CommandId = Guid.NewGuid(),
                AggregateId = contract, EventSource = "isolated-integration", ReceivedOn = timestamp,
                TickDataId = new(contract, date, count, timestamp), AssetTypeId = AssetTypeId.Futures,
                Dataset = "GLBX.MDP3", DefinitionDate = date, PublisherId = 1, InstrumentId = uint.MaxValue,
                EmissionReason = QuoteEmissionReason.BufferFull, QuoteCount = (ushort)count,
                QuoteData = new(quotes, (ushort)count)
            };
            await market.InsertTickQuoteDataAsync(quote);
            var json = await raw.UseTest("SELECT JSON * FROM tick_quote_data WHERE asset_type_id=? AND contract_id=? AND value_date=? AND aggregation_time=? AND sequence_id=?;")
                .SetParameters((object)new object[] { (sbyte)AssetTypeId.Futures, contract, date, TimeOnly.FromDateTime(timestamp), (long)count })
                .ExecuteQueryAsync(row => row.GetString(0));
            using var document = JsonDocument.Parse(Assert.Single(json));
            Assert.Equal(count, document.RootElement.GetProperty("quote_count").GetInt32());
            Assert.Equal(quote.Id, document.RootElement.GetProperty("actor_event_id").GetGuid());
            var savedQuotes = document.RootElement.GetProperty("quote_data");
            Assert.Equal(count, savedQuotes.GetArrayLength());
            for (var index = 0; index < count; index++)
            {
                var item = savedQuotes[index]; var expected = quotes[index];
                Assert.Equal((long)expected.SourceSequence, item.GetProperty("source_sequence").GetInt64());
                Assert.Equal(expected.EventTimestampNanoseconds, item.GetProperty("source_event_timestamp_ns").GetInt64());
                Assert.Equal(expected.ReceiveTimestampNanoseconds, item.GetProperty("source_receive_timestamp_ns").GetInt64());
                Assert.Equal((int)expected.HeaderFlags, item.GetProperty("header_flags").GetInt32());
                Assert.Equal(expected.BidPriceRaw, item.GetProperty("bid_price_raw").GetInt64());
                Assert.Equal(expected.BidPrice, item.GetProperty("bid_price").ValueKind == JsonValueKind.Null ? null : item.GetProperty("bid_price").GetDecimal());
                Assert.Equal((long)expected.BidSize, item.GetProperty("bid_size").GetInt64());
                Assert.Equal((long)expected.BidCount, item.GetProperty("bid_count").GetInt64());
                Assert.Equal(expected.AskPriceRaw, item.GetProperty("ask_price_raw").GetInt64());
                Assert.Equal(expected.AskPrice, item.GetProperty("ask_price").ValueKind == JsonValueKind.Null ? null : item.GetProperty("ask_price").GetDecimal());
                Assert.Equal((long)expected.AskSize, item.GetProperty("ask_size").GetInt64());
                Assert.Equal((long)expected.AskCount, item.GetProperty("ask_count").GetInt64());
            }
        }
    }
}
