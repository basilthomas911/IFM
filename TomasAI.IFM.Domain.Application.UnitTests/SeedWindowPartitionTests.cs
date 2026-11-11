using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class SeedWindowPartitionTests
{
    [Fact]
    public void Pooled_rows_are_cleared_returned_once_and_do_not_own_the_completed_bar()
    {
        var pool = new RowPool();
        var date = new DateOnly(2026, 11, 6);
        var start = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);
        var window = new MarketSessionBounds(date, start, start.AddSeconds(15));
        FuturesTradeSessionBarReadModel? bar;
        using (var buckets = new StoredSessionIndicatorSeedReader.SeedWindowBuckets([window], pool))
        {
            for (var index = 0; index < 100; index++)
            {
                var time = start.AddMilliseconds(index * 100);
                buckets.Add(new(date, 100 + index / 4m, 1, index, time.ToUnixTimeMilliseconds() * 1000000, 0, (short)'T', 0, 1, 1)
                { AggregationTimeUtc = TimeOnly.FromDateTime(time.UtcDateTime) });
            }
            bar = StoredSessionIndicatorSeedReader.AggregateCore("ES20261218", TimeFrameType.FifteenSeconds, window, start.AddMinutes(1), buckets.Rows[0], true);
            Assert.True(pool.Outstanding > 0);
        }
        Assert.Equal(0, pool.Outstanding);
        pool.PoisonReturns();
        Assert.NotNull(bar); Assert.Equal(100, bar.TradeCount);
        Assert.Equal(124.75m, bar.Close); Assert.Equal(11237.5m, bar.PriceVolumeSum);
    }

    [Fact]
    public void Legacy_unmapped_times_release_unused_range_buffers_before_individual_window_fallback()
    {
        var pool = new RowPool();
        var date = new DateOnly(2026, 11, 6);
        var start = new DateTimeOffset(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);
        using var buckets = new StoredSessionIndicatorSeedReader.SeedWindowBuckets([
            new(date, start, start.AddSeconds(15)), new(date, start.AddSeconds(15), start.AddSeconds(30))], pool);
        var row = new BootstrapTradeRow(date, 100, 1, 1, start.AddSeconds(1).ToUnixTimeMilliseconds() * 1000000, 0, (short)'T', 0, 1, 1);
        buckets.Add(row with { AggregationTimeUtc = new(15, 0, 1) });
        Assert.True(pool.Outstanding > 0);
        buckets.Add(row);
        Assert.True(buckets.HasUnmappedTimes); Assert.Equal(0, pool.Outstanding);
        buckets.Add(row with { AggregationTimeUtc = new(15, 0, 1) });
        Assert.Equal(0, pool.Outstanding);
    }

    sealed class RowPool : System.Buffers.ArrayPool<BootstrapTradeRow>
    {
        readonly HashSet<BootstrapTradeRow[]> active = [];
        readonly List<BootstrapTradeRow[]> returned = [];
        public int Outstanding => active.Count;
        public override BootstrapTradeRow[] Rent(int minimumLength)
        { var array = new BootstrapTradeRow[minimumLength]; Assert.True(active.Add(array)); return array; }
        public override void Return(BootstrapTradeRow[] array, bool clearArray = false)
        {
            Assert.True(active.Remove(array)); Assert.True(clearArray);
            Array.Clear(array); returned.Add(array);
        }
        public void PoisonReturns()
        { foreach (var array in returned) Array.Fill(array, new BootstrapTradeRow(default, -1, 0, 0, 0, 0, 0, 0, 0, 0)); }
    }

    [Theory]
    [InlineData(15)]
    [InlineData(60)]
    public void One_pass_partition_preserves_shuffled_ties_duplicates_invalid_rows_and_exact_decimal_outputs(int seconds)
    {
        var date = new DateOnly(2026, 11, 2);
        var start = new DateTimeOffset(2026, 11, 1, 23, 58, 0, TimeSpan.Zero);
        var windows = Enumerable.Range(0, 240 / seconds).Select(index => new MarketSessionBounds(date, start.AddSeconds(index * seconds), start.AddSeconds((index + 1) * seconds))).ToArray();
        var random = new Random(9127);
        var rows = Enumerable.Range(0, 5000).Select(index =>
        {
            var time = start.AddMilliseconds(random.Next(-60000, 300000));
            return new BootstrapTradeRow(date, 5000 + random.Next(100) / 4m, random.Next(1, 10), index % 100,
                time.ToUnixTimeMilliseconds() * 1000000, 0, (short)(index % 11 == 0 ? 'Q' : 'T'), 0, 1, 1)
                { AggregationTimeUtc = TimeOnly.FromDateTime(time.UtcDateTime) };
        }).ToArray();
        rows = rows.Concat(rows.Take(1000)).OrderBy(_ => random.Next()).ToArray();
        using var buckets = new StoredSessionIndicatorSeedReader.SeedWindowBuckets(windows);
        foreach (var row in rows) buckets.Add(row);
        Assert.False(buckets.HasUnmappedTimes);
        for (var index = 0; index < windows.Length; index++)
        {
            var before = StoredSessionIndicatorSeedReader.Aggregate("ES20261218", TimeFrameType.OneMinute, windows[index], start.AddMinutes(5), rows);
            var after = StoredSessionIndicatorSeedReader.AggregateCore("ES20261218", TimeFrameType.OneMinute, windows[index], start.AddMinutes(5), buckets.Rows[index], true);
            Assert.Equal(before, after);
        }
    }

    [Fact]
    public void Progress_revision_ignores_reconstruction_clock_but_invalidates_corrected_source_and_values()
    {
        var bar = new FuturesTradeSessionBarReadModel { ContractId = "ES20261218", Close = 100, CalculatedAtUtc = DateTimeOffset.UtcNow };
        var revision = StoredSessionIndicatorWarmup.SeedRevision([bar]);
        Assert.Equal(revision, StoredSessionIndicatorWarmup.SeedRevision([bar with { CalculatedAtUtc = bar.CalculatedAtUtc.AddMinutes(1) }]));
        Assert.NotEqual(revision, StoredSessionIndicatorWarmup.SeedRevision([bar with { Close = 101 }]));
        Assert.NotEqual(revision, StoredSessionIndicatorWarmup.SeedRevision([bar with { CalculationVersion = "corrected" }]));
        Assert.NotEqual(revision, StoredSessionIndicatorWarmup.SeedRevision([bar with { StreamEpochId = Guid.NewGuid() }]));
        Assert.NotEqual(revision, StoredSessionIndicatorWarmup.SeedRevision([bar with { LastSourceSequence = 1 }]));
        Assert.NotEqual(revision, StoredSessionIndicatorWarmup.SeedRevision([bar with { PriceVolumeSum = 100 }]));
        Assert.NotEqual(revision, StoredSessionIndicatorWarmup.SeedRevision([bar with { IsValid = true }]));
        Assert.NotEqual(revision, StoredSessionIndicatorWarmup.SeedRevision([bar with { FirstMarketEventUtc = DateTimeOffset.UtcNow }]));
    }
}
