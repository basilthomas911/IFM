using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
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
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

/// <summary>Checks durable restart lookup using the original trading-date month and exact interval identity.</summary>
public sealed class StoredSessionBarTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output) : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Theory]
    [InlineData(TimeFrameType.FifteenMinutes, 15)]
    [InlineData(TimeFrameType.FourHours, 240)]
    public async Task Immutable_bar_round_trips_across_a_trading_month_boundary_and_rejects_other_intervals(TimeFrameType frame, int minutes)
    {
        var baseConnection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Username = "", Password = "" }.ConnectionString;
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection, baseConnection, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAsync(["market_data"]);
        var factory = Substitute.For<IDbContextFactory>();
        var db = new MarketDataDbContext(settings, factory, Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        var end = new DateTimeOffset(2026, 4, 30, 23, 15, 0, TimeSpan.Zero);
        var date = new DateOnly(2026, 5, 1);
        var contract = "ES-" + Guid.NewGuid().ToString("N");
        var series = MarketSeriesIdentity.ForContract(contract);
        var bar = new FuturesTradeSessionBarReadModel
        {
            MarketSeriesIdentity = series, ContractId = contract, TimeFrame = frame, ValueDate = date,
            ObservationId = FuturesTradeSessionBarId.Create(series, frame, end, 9),
            IntervalStartUtc = end.AddMinutes(-minutes), IntervalEndUtc = end, Open = 100, High = 102, Low = 99, Close = 101,
            Volume = 20, FirstSourceSequence = 1, LastSourceSequence = 9,
            FirstMarketEventUtc = end.AddMinutes(1 - minutes), LastMarketEventUtc = end.AddSeconds(-1), CalculatedAtUtc = end,
            SchemaVersion = 1, CalculationVersion = "original-minute-test-v1", IsComplete = true, IsValid = true
        };
        Assert.True(await db.TryWriteObservationAsync(bar, default));
        Assert.False(await db.TryWriteObservationAsync(bar, default));
        var restarted = new MarketDataDbContext(settings, factory, Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        var restored = await restarted.GetStoredTradeSessionBarAsync(contract, bar.TimeFrame, date, end);
        var batch = await restarted.GetStoredTradeSessionBarsAsync(contract, frame, date, [end, end, end.AddMinutes(15)]);
        Assert.Equal(restored, Assert.Single(batch));
        Assert.Empty(await restarted.GetStoredTradeSessionBarsAsync(contract, frame, date.AddDays(-1), [end]));
        Assert.Equal(bar.ObservationId, restored!.ObservationId);
        Assert.Equal(bar.High, restored.High); Assert.Equal(bar.Volume, restored.Volume);
        Assert.Equal(bar.ValueDate, restored.ValueDate); Assert.Equal(bar.LastMarketEventUtc, restored.LastMarketEventUtc);
        Assert.Null(await db.GetStoredTradeSessionBarAsync(contract, bar.TimeFrame, date.AddDays(-1), end));
        Assert.Null(await db.GetStoredTradeSessionBarAsync(contract, TimeFrameType.OneHour, date, end));
        Assert.Null(await db.GetStoredTradeSessionBarAsync(contract, bar.TimeFrame, date, end.AddMinutes(15)));

        var corrected = bar with
        {
            ObservationId = FuturesTradeSessionBarId.Create(series, frame, end, 10),
            LastSourceSequence = 10, CalculatedAtUtc = end.AddSeconds(1), Close = 102
        };
        Assert.True(await db.TryWriteObservationAsync(corrected, default));
        Assert.Equal(corrected.ObservationId,
            Assert.Single(await restarted.GetStoredTradeSessionBarsAsync(contract, frame, date, [end])).ObservationId);

        if (frame == TimeFrameType.FifteenMinutes && Environment.GetEnvironmentVariable("IFM_TIME_STORED_BARS") == "1")
            await TimeLookupsAsync(restarted, bar);
    }

    [Fact]
    public async Task Qualified_seed_coverage_survives_restart_and_keeps_provider_versions_isolated()
    {
        var baseConnection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Username = "", Password = "" }.ConnectionString;
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection, baseConnection, "System.Data.Postgres");
        var schema = new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance);
        await schema.CreateAsync(["market_data"]);
        await schema.CreateAsync(["indicator_seed_coverage"]);
        var factory = Substitute.For<IDbContextFactory>();
        MarketDataDbContext Open() => new(settings, factory,
            Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);

        var db = Open();
        var contract = "ES-" + Guid.NewGuid().ToString("N");
        var date = new DateOnly(2026, 5, 1);
        var end = new DateTimeOffset(2026, 4, 30, 23, 15, 0, TimeSpan.Zero);
        var bar = SeedBar(contract, date, end);
        var changed = bar with { Close = 102 };
        var next = SeedBar(contract, date, end.AddMinutes(1));
        var legacy = bar with { Close = 99 };
        Assert.True(await db.TryWriteObservationAsync(legacy, default));
        Assert.Null(await db.GetStoredTradeSessionBarAsync(contract, bar.TimeFrame, date, end));
        await db.StoreQualifiedIndicatorSeedBarsAsync("source-a", [bar, next], default);
        await db.StoreQualifiedIndicatorSeedBarsAsync("source-b", [changed], default);
        await db.StoreQualifiedIndicatorSeedBarsAsync("source-a", [changed], default);

        var restarted = Open();
        var first = await restarted.GetQualifiedIndicatorSeedBarsAsync(contract, bar.TimeFrame, date, [end, next.IntervalEndUtc], "source-a", default);
        Assert.Equal(new[] { bar, next }, first.OrderBy(value => value.IntervalEndUtc));
        var second = await restarted.GetQualifiedIndicatorSeedBarsAsync(contract, bar.TimeFrame, date, [end, next.IntervalEndUtc], "source-b", default);
        Assert.Equal(changed, Assert.Single(second));
        Assert.Empty(await restarted.GetQualifiedIndicatorSeedBarsAsync(contract, bar.TimeFrame, date, [end], "source-c", default));
        Assert.Empty(await restarted.GetQualifiedIndicatorSeedBarsAsync(contract, bar.TimeFrame, date.AddDays(-1), [end], "source-a", default));
        Assert.Empty(await restarted.GetQualifiedIndicatorSeedBarsAsync(contract, TimeFrameType.FifteenMinutes, date, [end], "source-a", default));

        Assert.Throws<ArgumentException>(() =>
            IndicatorSeedRequestQualification.RequireBatch("source-c", [bar, next with { IsComplete = false }]));
        Assert.Empty(await restarted.GetQualifiedIndicatorSeedBarsAsync(contract, bar.TimeFrame, date, [end], "source-c", default));

        var official = changed with
        {
            LastSourceSequence = 10,
            ObservationId = FuturesTradeSessionBarId.Create(bar.MarketSeriesIdentity, bar.TimeFrame, end, 10),
            CalculationVersion = "official-minute",
            CalculatedAtUtc = end.AddSeconds(1)
        };
        Assert.True(await db.TryWriteObservationAsync(official, default));
        Assert.Equal(official, Assert.Single(await restarted.GetQualifiedIndicatorSeedBarsAsync(contract, bar.TimeFrame, date, [end], "source-a", default)));

        if (Environment.GetEnvironmentVariable("IFM_TIME_SEED_COVERAGE") == "1")
            await TimeCoverageWritesAsync(db, date, end);
    }

    async Task TimeCoverageWritesAsync(MarketDataDbContext db, DateOnly date, DateTimeOffset end)
    {
        for (var pair = 0; pair <= 5; pair++)
        {
            foreach (var batch in pair % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                var contract = "ES-" + Guid.NewGuid().ToString("N");
                var rows = Enumerable.Range(0, 48).Select(index => SeedBar(contract, date, end.AddMinutes(index))).ToArray();
                var began = Stopwatch.GetTimestamp();
                if (batch)
                    await db.StoreQualifiedIndicatorSeedBarsAsync("source-a", rows, default);
                else
                    foreach (var row in rows)
                        Assert.True(await db.TryWriteObservationAsync(row, default));
                if (pair > 0)
                    output.WriteLine($"SEED_COVERAGE_WRITE pair={pair} batch={batch} ms={Stopwatch.GetElapsedTime(began).TotalMilliseconds:F3} calls={(batch ? 1 : 48)}");
            }
        }
    }

    static FuturesTradeSessionBarReadModel SeedBar(string contract, DateOnly date, DateTimeOffset end)
    {
        var series = MarketSeriesIdentity.ForContract(contract);
        return new()
        {
            MarketSeriesIdentity = series, ContractId = contract, TimeFrame = TimeFrameType.OneMinute, ValueDate = date,
            ObservationId = FuturesTradeSessionBarId.Create(series, TimeFrameType.OneMinute, end, 9),
            IntervalStartUtc = end.AddMinutes(-1), IntervalEndUtc = end, Open = 100, High = 102, Low = 99, Close = 101,
            Volume = 20, TradeCount = 2, PriceVolumeSum = 2010, FirstSourceSequence = 1, LastSourceSequence = 9,
            FirstMarketEventUtc = end.AddSeconds(-55), LastMarketEventUtc = end.AddSeconds(-1), CalculatedAtUtc = end,
            SchemaVersion = 1, CalculationVersion = "databento-four-hour-trade-replay-v1", IsComplete = true, IsValid = true,
            CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
        };
    }

    async Task TimeLookupsAsync(MarketDataDbContext db, FuturesTradeSessionBarReadModel original)
    {
        var ends = Enumerable.Range(0, 48).Select(index => original.IntervalEndUtc.AddMinutes(15 * index)).ToArray();
        foreach (var end in ends.Skip(1))
        {
            var offset = end - original.IntervalEndUtc;
            Assert.True(await db.TryWriteObservationAsync(original with
            {
                ObservationId = FuturesTradeSessionBarId.Create(original.MarketSeriesIdentity, original.TimeFrame, end, original.LastSourceSequence),
                IntervalStartUtc = original.IntervalStartUtc.Add(offset), IntervalEndUtc = end,
                FirstMarketEventUtc = original.FirstMarketEventUtc.Add(offset), LastMarketEventUtc = original.LastMarketEventUtc.Add(offset),
                CalculatedAtUtc = end
            }, default));
        }

        for (var sample = 0; sample <= 20; sample++)
        {
            var started = Stopwatch.GetTimestamp();
            var singular = new List<FuturesTradeSessionBarReadModel?>();
            foreach (var end in ends)
                singular.Add(await db.GetStoredTradeSessionBarAsync(original.ContractId, original.TimeFrame, original.ValueDate, end));
            var before = Stopwatch.GetElapsedTime(started);

            started = Stopwatch.GetTimestamp();
            var batch = await db.GetStoredTradeSessionBarsAsync(original.ContractId, original.TimeFrame, original.ValueDate, ends);
            var after = Stopwatch.GetElapsedTime(started);
            Assert.Equal(singular, batch);
            if (sample > 0) output.WriteLine($"STORED_BARS_TIMING beforeMs={before.TotalMilliseconds:F3} afterMs={after.TotalMilliseconds:F3}");
        }
    }
}
