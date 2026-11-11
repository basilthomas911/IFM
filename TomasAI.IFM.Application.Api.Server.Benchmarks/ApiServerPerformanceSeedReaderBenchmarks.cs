using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Full original and current API reader over identical recorded raw rows. Includes materialization/streaming, partitioning and aggregation; excludes the Scylla network.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig)), InvocationCount(1)]
public class ApiServerPerformanceSeedReaderBenchmarks
{
    [Params(1000, 100000, 300000)] public int Trades { get; set; }
    ApiServerPerformanceSeedReaderBaseline before = null!;
    StoredSessionIndicatorSeedReader after = null!;
    IDbContextFactory databases = null!;
    IMarketDataDbContext market = null!;
    public RawRows Source { get; private set; } = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var clock = new Clock();
        var calendar = new CmeFuturesMarketSessionCalendar();
        var windows = RsiHistoricalSeedWindowModel.Create(TimeFrameType.FifteenSeconds, 48, clock.GetUtcNow(), calendar);
        var random = new Random(9161);
        var duration = (int)(windows[^1].EndUtc - windows[0].StartUtc).TotalMilliseconds;
        var rows = Enumerable.Range(0, Trades).Select(index =>
        {
            var time = windows[0].StartUtc.AddMilliseconds(random.Next(duration));
            return new BootstrapTradeRow(windows[0].ValueDate, 5000 + random.Next(100) / 4m, 1, index,
                time.ToUnixTimeMilliseconds() * 1000000, 0, (short)'T', 0, 1, 1)
                { AggregationTimeUtc = TimeOnly.FromDateTime(time.UtcDateTime) };
        }).ToArray();
        var storage = DispatchProxy.Create<IStreamingContext, RawRows>();
        Source = (RawRows)(object)storage; Source.Rows = rows;
        databases = Substitute.For<IDbContextFactory>(); market = Substitute.For<IMarketDataDbContext>();
        databases.TradeTickQuoteDb.Returns(storage); databases.MarketDataDb.Returns(market);
        market.GetStoredTradeSessionBarsAsync(Arg.Any<string>(), Arg.Any<TimeFrameType>(), Arg.Any<DateOnly>(), Arg.Any<DateTimeOffset[]>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FuturesTradeSessionBarReadModel>>([]));
        before = new(databases, calendar, clock, NullLogger<ApiServerPerformanceSeedReaderBaseline>.Instance);
        after = new(databases, calendar, clock, NullLogger<StoredSessionIndicatorSeedReader>.Instance);
        var original = await ReadBefore(); var candidate = await ReadAfter();
        if (original.Length != 48 || !original.SequenceEqual(candidate)) throw new InvalidOperationException("Full seed reader output differs.");
    }

    [IterationSetup]
    public void Reset()
    {
        databases.ClearReceivedCalls(); market.ClearReceivedCalls();
        Source.MaterializedReads = 0; Source.StreamedReads = 0; Source.RowsDelivered = 0;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("11-12-FullSeedReaderRecordedRows")]
    public Task<FuturesTradeSessionBarReadModel[]> ReadBefore() => before.ReadAsync("ES20261218", TimeFrameType.FifteenSeconds, 48, default);
    [Benchmark, BenchmarkCategory("11-12-FullSeedReaderRecordedRows")]
    public Task<FuturesTradeSessionBarReadModel[]> ReadAfter() => after.ReadAsync("ES20261218", TimeFrameType.FifteenSeconds, 48, default);

    /// <summary>Separate all-thread resource probe; process peaks include identical fixture/equivalence warm-up and are sampled every ten milliseconds.</summary>
    public async Task<object> ResourceProbeAsync(bool candidate)
    {
        for (var index = 0; index < 20; index++)
            if ((await (candidate ? ReadAfter() : ReadBefore())).Length != 48) throw new InvalidOperationException("Incomplete warm-up seed.");
        Reset();
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        long peak = 0, privatePeak = 0;
        using var stop = new CancellationTokenSource();
        var sampler = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    process.Refresh(); peak = Math.Max(peak, process.WorkingSet64); privatePeak = Math.Max(privatePeak, process.PrivateMemorySize64);
                    await Task.Delay(10, stop.Token);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        });
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var paused = GC.GetTotalPauseDuration(); var allocated = GC.GetTotalAllocatedBytes(true);
        using var timingProcess = System.Diagnostics.Process.GetCurrentProcess();
        var cpuBefore = timingProcess.TotalProcessorTime;
        var began = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var index = 0; index < 20; index++)
            if ((await (candidate ? ReadAfter() : ReadBefore())).Length != 48) throw new InvalidOperationException("Incomplete seed.");
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(began);
        timingProcess.Refresh();
        var cpu = timingProcess.TotalProcessorTime - cpuBefore;
        allocated = GC.GetTotalAllocatedBytes(true) - allocated;
        var gc = Enumerable.Range(0, 3).Select(index => GC.CollectionCount(index) - collections[index]).ToArray();
        var pause = GC.GetTotalPauseDuration() - paused;
        await stop.CancelAsync(); await sampler;
        return new { Candidate = candidate, Trades, WarmupReads = 20, Reads = 20,
            ServerGc = System.Runtime.GCSettings.IsServerGC, ProcessorCount = Environment.ProcessorCount,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ElapsedMilliseconds = elapsed.TotalMilliseconds, CpuMilliseconds = cpu.TotalMilliseconds,
            AllocatedBytesAllThreads = allocated, AllocatedBytesPerRead = allocated / 20d,
            Gen0 = gc[0], Gen1 = gc[1], Gen2 = gc[2], GcPauseMilliseconds = pause.TotalMilliseconds,
            SampledPeakWorkingSetBytes = peak, SampledPeakPrivateBytes = privatePeak,
            Source.MaterializedReads, Source.StreamedReads, Source.RowsDelivered,
            Note = "Recorded-row full reader; Scylla network excluded. Identical fixture/equivalence warm-up included in process footprint. Ten-millisecond sampling can miss shorter peaks. Sampler allocations are included symmetrically." };
    }

    public interface IStreamingContext : ITradeTickQuoteDbContext, IStreamingRawFuturesTradeReader { }

    public class RawRows : DispatchProxy
    {
        public BootstrapTradeRow[] Rows = [];
        public int MaterializedReads, StreamedReads;
        public long RowsDelivered;
        protected override object? Invoke(MethodInfo? method, object?[]? arguments)
        {
            if (method?.Name == nameof(ITradeTickQuoteDbContext.GetRawFuturesTradeWindowAsync))
            {
                MaterializedReads++;
                var rows = Filter(arguments!).ToArray(); RowsDelivered += rows.Length;
                return Task.FromResult<IReadOnlyList<BootstrapTradeRow>>(rows);
            }
            if (method?.Name == "get_SupportsRawFuturesTradeStreaming") return this is IStreamingContext;
            if (method?.Name == nameof(IStreamingRawFuturesTradeReader.StreamRawFuturesTradeWindowAsync))
            {
                StreamedReads++;
                return Stream(Filter(arguments!), (CancellationToken)arguments![4]!);
            }
            throw new NotSupportedException(method?.Name);
        }
        IEnumerable<BootstrapTradeRow> Filter(object?[] arguments)
        {
            var date = (DateOnly)arguments[1]!; var lower = (TimeOnly)arguments[2]!; var upper = (TimeOnly)arguments[3]!;
            return Rows.Where(row => row.ValueDate == date && row.AggregationTimeUtc is { } time && time >= lower && time <= upper);
        }
        async IAsyncEnumerable<BootstrapTradeRow> Stream(IEnumerable<BootstrapTradeRow> rows,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            foreach (var row in rows) { token.ThrowIfCancellationRequested(); RowsDelivered++; yield return row; }
            await Task.CompletedTask;
        }
    }
    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 11, 2, 0, 5, 0, TimeSpan.Zero); }
}
