using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Verifies that emulator historical quotes retain a coherent session and their true observation times.</summary>
[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresEmulatorQuoteSnapshotTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Latest_snapshot_excludes_recent_quotes_future_dates_and_other_contracts()
    {
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection,
            fixture.Connection, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var factory = Substitute.For<IDbContextFactory>();
        var db = new MarketDataDbContext(settings, factory, Substitute.For<IBlackboardService>(),
            Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        factory.MarketDataDb.Returns(db);
        var underlying = $"EMULATOR{Guid.NewGuid():N}";
        var expiry = new DateOnly(2026, 11, 20);
        var close = new DateTimeOffset(2026, 11, 5, 21, 59, 0, TimeSpan.Zero);
        var date = DateOnly.FromDateTime(close.UtcDateTime);
        var call = new FuturesOptionTickDataV2ReadModel {
            ContractId = "ES20261120C8050", ValueDate = date,
            TickId = (close - DateTimeOffset.UnixEpoch).Ticks * 100,
            TickTime = new(21, 59), BidPrice = .8, AskPrice = 1.2
        };
        var put = call with { ContractId = "ES20261120P7375", BidPrice = 156.5, AskPrice = 159 };
        await db.UpsertFuturesOptionChainQuoteDataAsync(underlying, call with { ValueDate = date.AddDays(-1) });
        await db.UpsertFuturesOptionChainQuoteDataAsync(underlying, call);
        await db.UpsertFuturesOptionChainQuoteDataAsync(underlying, put);
        await db.UpsertFuturesOptionChainQuoteDataAsync(underlying + "OTHER", call);
        await db.UpsertFuturesOptionChainQuoteDataAsync(underlying,
            call with { ValueDate = date.AddDays(1), TickId = call.TickId + TimeSpan.TicksPerDay * 100, BidPrice = 99 });
        // A newer date containing only an invalid book must not hide the valid closing snapshot.
        await db.UpsertFuturesOptionChainQuoteDataAsync(underlying,
            put with { ValueDate = date.AddDays(2), BidPrice = 0, AskPrice = 0 });

        var rows = await db.GetLatestFuturesOptionChainQuoteDataAsync(underlying, expiry, date.AddDays(3), close);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(date, row.ValueDate));
        Assert.Equal(.8, rows.Single(row => row.ContractId == call.ContractId).BidPrice);
        Assert.Equal(156.5, rows.Single(row => row.ContractId == put.ContractId).BidPrice);
        Assert.All(rows, row => Assert.Equal(call.TickId, row.TickId));
        Assert.Empty(await db.GetLatestFuturesOptionChainQuoteDataAsync(underlying, expiry.AddDays(1), date, close));
        Assert.Empty(await db.GetLatestFuturesOptionChainQuoteDataAsync(underlying, expiry, date.AddDays(-2), close));
    }
}
