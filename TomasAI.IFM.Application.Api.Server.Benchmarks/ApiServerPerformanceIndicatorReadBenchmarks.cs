using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Real PostgreSQL cold/missing read-model lookup: identical six queries and bindings, five independent reads bounded to two followed by Bollinger.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceIndicatorReadBenchmarks
{
    readonly ApiServerPerformanceDatabaseFixture database = new();
    readonly DateOnly date = new(2026, 11, 6);
    Func<Task>[] independent = null!;
    [GlobalSetup]
    public async Task Setup()
    {
        await database.InitializeAsync();
        var db = database.Market;
        independent = [
            async () => { _ = await db.GetLastFuturesAdxSignalAsync("ES-perf-missing", date, TimeFrameType.OneHour, 14, default); },
            async () => { _ = await db.GetLastFuturesAtrSignalAsync("ES-perf-missing", date, TimeFrameType.OneHour, 14, default); },
            async () => { _ = await db.GetLastFuturesMacdSignalAsync("ES-perf-missing", date, TimeFrameType.OneHour, 9, 12, 26, default); },
            async () => { _ = await db.GetLastFuturesRsiSignalAsync("ES-perf-missing", date, TimeFrameType.OneHour, 14, default); },
            async () => { _ = await db.GetLastFuturesTdiSignalAsync("ES-perf-missing", date, TimeFrameType.OneHour, FuturesTdiConfiguration.StandardConfigurationId, default); }
        ];
    }

    [Benchmark(Baseline = true), BenchmarkCategory("15-PostgresIndicatorReadRestoration")]
    public async Task SerialBefore()
    {
        foreach (var read in independent) await read();
        await Bollinger();
    }

    [Benchmark, BenchmarkCategory("15-PostgresIndicatorReadRestoration")]
    public async Task BoundedAfter()
    {
        using var gate = new SemaphoreSlim(2, 2);
        await Task.WhenAll(independent.Select(async read =>
        {
            await gate.WaitAsync();
            try { await read(); } finally { gate.Release(); }
        }));
        await Bollinger();
    }

    async Task Bollinger() => _ = await database.Market.GetLatestFuturesBollingerBandSignalForTimeFrameAsync(
        MarketSeriesIdentity.ForContract("ES-perf-missing"), date, TimeFrameType.OneHour, default);

    [GlobalCleanup] public async Task Cleanup() => await database.DisposeAsync();
}
