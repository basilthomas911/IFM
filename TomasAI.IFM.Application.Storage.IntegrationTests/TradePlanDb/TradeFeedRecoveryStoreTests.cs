using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class TradeFeedRecoveryStoreTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Recovery_contracts_survive_new_store_instance_and_off_deletes_only_that_trade_and_date()
    {
        var settings=(DbConnectionSettings)new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,fixture.Connection,"System.Data.Postgres");
        var schema = new TradeSchemaDb(settings,NullLogger<DbProvider>.Instance);
        // Exercise the narrow normal-startup migration, not full deployment initialization.
        await schema.CreateAsync(["live_feed_recovery"]);
        await schema.CreateAsync(["live_feed_recovery"]);
        var store=new TradeFeedRecoveryStore(settings);var date=new DateOnly(2026,10,22);
        var registration=new TradeFeedRecoveryRegistration("101.701.1901.1501",date,["ES-P1","ES-P2","ES-C1","ES-C2"],DateTimeOffset.UtcNow);
        await store.SaveAsync(registration,default); await store.SaveAsync(registration,default);
        var restarted=new TradeFeedRecoveryStore(settings);
        Assert.Equal(4,Assert.Single(await restarted.ReadAsync(date,default)).OptionContracts.Length);
        await store.SaveAsync(registration with{ValueDate=date.AddDays(1)},default);
        await store.SaveAsync(registration with{TradeId="101.701.1901.1502"},default);
        await store.SaveAsync(registration with{OptionContracts=["ES-P3","ES-C3"]},default);
        Assert.Equal(2,(await store.ReadAsync(date,default)).Single(row=>row.TradeId==registration.TradeId).OptionContracts.Length);
        await store.RemoveAsync(registration.TradeId,date,default);
        Assert.Single(await store.ReadAsync(date,default)); Assert.Single(await store.ReadAsync(date.AddDays(1),default));
        await store.CloseAsync(date,default); Assert.Empty(await store.ReadAsync(date,default)); Assert.Single(await store.ReadAsync(date.AddDays(1),default));
        await store.CloseAsync(date.AddDays(1),default);
    }
}
