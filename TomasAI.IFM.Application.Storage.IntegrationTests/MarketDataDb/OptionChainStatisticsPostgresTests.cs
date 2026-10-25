using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;
namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class OptionChainStatisticsPostgresTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Saved_statistics_select_each_latest_field_independently_preserve_zero_and_exclude_future_dates()
    {
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection, fixture.Connection, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
        var underlying = "option-stats-"+Guid.NewGuid().ToString("N");
        var expiry = new DateOnly(2026,11,20);var date = new DateOnly(2026,10,14);
        async Task Insert(string id,DateOnly storedDate,long? volume,DateOnly? volumeDate,long? oi,DateOnly? oiDate) =>
            await fixture.Transactions.ExecuteOnceAsync(async (tx,ct)=>{
                await tx.ExecuteAsync("INSERT INTO market_data.futures_option_chain_quote_data(underlyingContractId,expiryDate,valueDate,contractId,volume,volumeValueDate,openInterest,openInterestValueDate) VALUES($1,$2,$3,$4,$5,$6,$7,$8)",
                    [underlying,expiry,storedDate,id,volume,volumeDate,oi,oiDate],ct);return true;});
        await Insert("call",date.AddDays(-1),30,date.AddDays(-1),500,date.AddDays(-2));
        await Insert("call",date,40,date,null,null);
        await Insert("call",date.AddDays(1),999,date.AddDays(1),999,date.AddDays(1));
        await Insert("put",date,0,date,0,date.AddDays(-1));
        await Insert("future-reference",date,999,date.AddDays(1),999,date.AddDays(1));
        var db = MarketDataDbContextTestFactory.Create(settings[MarketDataDbContext.MarketDataDbConnection]);
        var rows = await db.ReadOptionChainStatisticsAsync(underlying,expiry,date,CancellationToken.None);
        Assert.Equal(2,rows.Length);
        var call = rows.Single(x=>x.ContractId=="call");
        Assert.Equal(40,call.Volume);Assert.Equal(date,call.VolumeValueDate);
        Assert.Equal(500,call.OpenInterest);Assert.Equal(date.AddDays(-2),call.OpenInterestValueDate);
        var put = rows.Single(x=>x.ContractId=="put");Assert.Equal(0,put.Volume);Assert.Equal(0,put.OpenInterest);
        Assert.Empty(await db.ReadOptionChainStatisticsAsync(underlying,expiry.AddDays(1),date,CancellationToken.None));
    }
}
