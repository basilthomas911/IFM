using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Checks durable restart lookup using the original trading-date month and exact interval identity.</summary>
public sealed class StoredSessionBarTests(EventSourceActorSnapshotRangeFixture fixture) : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Immutable_bar_round_trips_across_a_trading_month_boundary_and_rejects_other_intervals()
    {
        var baseConnection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Username = "", Password = "" }.ConnectionString;
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection, baseConnection, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAsync(["market_data"]);
        var factory = Substitute.For<IDbContextFactory>();
        var db = new MarketDataDbContext(settings, factory, Substitute.For<IBlackboardService>(), Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        var end = new DateTimeOffset(2026, 4, 30, 23, 15, 0, TimeSpan.Zero);
        var date = new DateOnly(2026, 5, 1);
        var contract = "ES-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var bar = new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series, ContractId = contract, TimeFrame = TimeFrameType.FifteenMinutes, ValueDate = date,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.FifteenMinutes, end, 9),
            IntervalStartUtc = end.AddMinutes(-15), IntervalEndUtc = end, Open = 100, High = 102, Low = 99, Close = 101,
            Volume = 20, FirstSourceSequence = 1, LastSourceSequence = 9,
            FirstMarketEventUtc = end.AddMinutes(-14), LastMarketEventUtc = end.AddSeconds(-1), CalculatedAtUtc = end,
            SchemaVersion = 1, CalculationVersion = "original-minute-test-v1", IsComplete = true, IsValid = true
        };
        Assert.True(await db.TryWriteObservationAsync(bar, default));
        Assert.False(await db.TryWriteObservationAsync(bar, default));
        var restored = await db.GetStoredTradeSessionBarAsync(contract, bar.TimeFrame, date, end);
        Assert.Equal(bar.ObservationId, restored!.ObservationId);
        Assert.Equal(bar.High, restored.High); Assert.Equal(bar.Volume, restored.Volume);
        Assert.Equal(bar.ValueDate, restored.ValueDate); Assert.Equal(bar.LastMarketEventUtc, restored.LastMarketEventUtc);
        Assert.Null(await db.GetStoredTradeSessionBarAsync(contract, bar.TimeFrame, date.AddDays(-1), end));
        Assert.Null(await db.GetStoredTradeSessionBarAsync(contract, TimeFrameType.OneHour, date, end));
        Assert.Null(await db.GetStoredTradeSessionBarAsync(contract, bar.TimeFrame, date, end.AddMinutes(15)));
    }
}
