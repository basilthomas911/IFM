using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceSeedBenchmarks
{
    [Params(1000, 100000)] public int Trades { get; set; }
    BootstrapTradeRow[] rows = null!;
    MarketSessionBounds[] windows = null!;
    readonly DateTimeOffset cutoff = new(2026, 11, 2, 0, 5, 0, TimeSpan.Zero);

    [GlobalSetup]
    public void Setup()
    {
        var date = new DateOnly(2026, 11, 2);
        var start = cutoff.AddMinutes(-5);
        windows = Enumerable.Range(0, 16).Select(index => new MarketSessionBounds(date, start.AddSeconds(index * 15), start.AddSeconds((index + 1) * 15))).ToArray();
        var random = new Random(9127);
        rows = Enumerable.Range(0, Trades).Select(index =>
        {
            var time = start.AddMilliseconds(random.Next(240000));
            return new BootstrapTradeRow(date, 5000 + random.Next(100) / 4m, 1, index, time.ToUnixTimeMilliseconds() * 1000000,
                0, (short)'T', 0, 1, 1) { AggregationTimeUtc = TimeOnly.FromDateTime(time.UtcDateTime) };
        }).ToArray();
        if (!AggregateBefore().SequenceEqual(AggregateAfter())) throw new InvalidOperationException("Seed output mismatch.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("11-12-SeedAggregation")]
    public FuturesTradeSessionBarReadModel?[] AggregateBefore() => windows.Select(window =>
        ApiServerPerformanceSeedBaseline.Aggregate("ES20261218", TimeFrameType.FifteenSeconds, window, cutoff, rows)).ToArray();

    [Benchmark, BenchmarkCategory("11-12-SeedAggregation")]
    public FuturesTradeSessionBarReadModel?[] AggregateAfter()
    {
        using var buckets = new StoredSessionIndicatorSeedReader.SeedWindowBuckets(windows);
        foreach (var row in rows) buckets.Add(row);
        return windows.Select((window, index) => StoredSessionIndicatorSeedReader.AggregateCore("ES20261218",
            TimeFrameType.FifteenSeconds, window, cutoff, buckets.Rows[index], true)).ToArray();
    }
}
