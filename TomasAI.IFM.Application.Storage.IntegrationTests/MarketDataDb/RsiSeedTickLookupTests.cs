using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Runs against a disposable isolated CQL keyspace; never uses an application keyspace.</summary>
public sealed class RsiSeedTickLookupTests
{
    [Fact]
    public async Task Indexed_lookup_returns_nearest_preceding_tick_and_never_a_future_tick()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("rsi-seed-tick-lookup");
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add("TradeTickQuoteDbConnection", infrastructure.CqlConnectionString, "System.Data.ScyllaDb");
        var logger = Substitute.For<ILogger<DbProvider>>();
        await new Storage.TradeTickQuoteDb.Schema.TradeTickQuoteSchemaDb(settings, logger).CreateAllAsync();
        var db = new Storage.TradeTickQuoteDb.TradeTickQuoteDbContext(settings, logger);
        var contract = "RSI-SEED-" + Guid.NewGuid().ToString("N"); var date = new DateOnly(2026, 8, 25);
        var rows = new[]{SampleData.FuturesTickData with {ContractId=contract,ValueDate=date,TickId=1,TickTime=new(12,0,0),Price=100m},
   SampleData.FuturesTickData with {ContractId=contract,ValueDate=date,TickId=2,TickTime=new(12,1,0),Price=101m}};
        await db.InsertFuturesTickDataAsync(rows);
        Assert.Equal(1, (await db.GetFuturesTickAtOrBeforeAsync(contract, date, new(12, 0, 30)))!.TickId);
        Assert.Equal(2, (await db.GetFuturesTickAtOrBeforeAsync(contract, date, new(12, 1, 0)))!.TickId);
        Assert.Null(await db.GetFuturesTickAtOrBeforeAsync(contract, date, new(11, 59, 59)));
        Assert.Null(await db.GetFuturesTickAtOrBeforeAsync(contract, date.AddDays(1), new(12, 1, 0)));
    }
}
