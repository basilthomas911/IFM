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
    }
}
