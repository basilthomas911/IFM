using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Measured batches include identical thread wakeup/completion costs. Batch means are not individual request percentiles.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, Config(typeof(ApiServerPerformanceConfig))]
public class ApiServerPerformanceTradeContentionBenchmarks
{
    [Params(1, 4, 16)] public int Producers { get; set; }
    const int WritesPerProducer = 64;
    readonly List<Thread> threads = [];
    AutoResetEvent[] starts = null!;
    CountdownEvent finished = null!;
    ApiServerPerformanceTradeCacheBaseline before = null!;
    TradeRealtimeHotCache after = null!;
    ApiServerPerformanceExpiryBaseline beforeRegistry = null!;
    TradeRealtimeCacheRegistry afterRegistry = null!;
    long quoteSequence;
    FuturesOptionTickDataV2ReadModel quote = null!;
    volatile bool candidate;
    volatile bool stopped;
    long[][]? requestTicks;
    int[] requestCursors = [];

    [GlobalSetup]
    public void Setup()
    {
        var clock = TimeProvider.System;
        var date = new DateOnly(2026, 11, 7);
        var trade = new EstablishedTradeDefinition { Id = new(101, 701, 1901, 1501), StrategyKind = TradeStrategyKind.IronCondor,
            Legs = Enumerable.Range(0, 4).Select(index => new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = "option-" + index,
                SignedQuantity = index % 2 == 0 ? -1 : 1, CashMultiplier = 50 }).ToArray() };
        beforeRegistry = new(clock); afterRegistry = new(clock);
        before = beforeRegistry.Register(trade, date); after = afterRegistry.Register(trade, date);
        quote = new("option-0", date, 1, new(12, 0), 10, 9, 11, 1, 1, .2, 5000, .4, .01, 12, -3, .1)
        { ObservedAtUtc = clock.GetUtcNow(), ValidUntilUtc = clock.GetUtcNow().AddSeconds(5), SourceGeneration = Guid.NewGuid() };
        before.Apply(quote); after.Apply(quote);
        starts = Enumerable.Range(0, Producers).Select(_ => new AutoResetEvent(false)).ToArray();
        finished = new(Producers);
        for (var index = 0; index < Producers; index++)
        {
            var workerIndex = index;
            var thread = new Thread(() => Work(workerIndex)) { IsBackground = true, Name = "Trade contention fixture" };
            threads.Add(thread); thread.Start();
        }
    }

    void Work(int index)
    {
        while (true)
        {
            starts[index].WaitOne();
            if (stopped) return;
            try
            {
                for (var iteration = 0; iteration < WritesPerProducer; iteration++)
                {
                    var captured = requestTicks;
                    var started = captured is null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
                    // Identical immutable incoming quote construction is included on both
                    // paths. Fresh deadlines exercise the registered scheduler callback too.
                    var now = DateTimeOffset.UtcNow;
                    var incoming = quote with { TickId = Interlocked.Increment(ref quoteSequence), ObservedAtUtc = now, ValidUntilUtc = now.AddSeconds(5) };
                    if (candidate) { after.Apply(incoming); _ = after.Read(); }
                    else { before.Apply(incoming); _ = before.Read(); }
                    if (captured is not null) captured[index][requestCursors[index]++] = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                }
            }
            finally { finished.Signal(); }
        }
    }

    void Batch(bool useCandidate)
    {
        candidate = useCandidate;
        finished.Reset(Producers);
        foreach (var start in starts) start.Set();
        if (!finished.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Contention fixture stalled.");
    }

    [Benchmark(Baseline = true), BenchmarkCategory("06-TradeCriticalSectionQualification")]
    public void BatchBefore() => Batch(false);
    [Benchmark, BenchmarkCategory("06-TradeCriticalSectionQualification")]
    public void BatchAfter() => Batch(true);

    /// <summary>Separate all-thread resource and real per-request latency probe; instrumentation is excluded from BenchmarkDotNet methods.</summary>
    public object ResourceProbe(bool useCandidate, int batches = 100)
    {
        for (var index = 0; index < 20; index++) Batch(useCandidate);
        requestCursors = new int[Producers];
        requestTicks = Enumerable.Range(0, Producers).Select(_ => new long[batches * WritesPerProducer]).ToArray();
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var allocations = GC.GetTotalAllocatedBytes(true);
        var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var pause = GC.GetTotalPauseDuration();
        var contentions = Monitor.LockContentionCount;
        long sampledPeak = 0;
        var cpuBefore = process.TotalProcessorTime;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var index = 0; index < batches; index++)
        {
            Batch(useCandidate);
            process.Refresh(); sampledPeak = Math.Max(sampledPeak, process.WorkingSet64);
        }
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        process.Refresh();
        var cpu = process.TotalProcessorTime - cpuBefore;
        var allocated = GC.GetTotalAllocatedBytes(true) - allocations;
        var gc = Enumerable.Range(0, 3).Select(index => GC.CollectionCount(index) - collections[index]).ToArray();
        var paused = GC.GetTotalPauseDuration() - pause;
        var contended = Monitor.LockContentionCount - contentions;
        var values = requestTicks.SelectMany(ticks => ticks).Order().ToArray();
        double Percentile(double percentile) => values[(int)Math.Ceiling((values.Length - 1) * percentile)] * 1_000_000d / System.Diagnostics.Stopwatch.Frequency;
        var result = new
        {
            Candidate = useCandidate, Producers, Batches = batches, Requests = values.Length,
            ServerGc = System.Runtime.GCSettings.IsServerGC, ProcessorCount = Environment.ProcessorCount,
            Runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            ElapsedMilliseconds = elapsed.TotalMilliseconds, CpuMilliseconds = cpu.TotalMilliseconds, AllocatedBytesAllThreads = allocated,
            AllocatedBytesPerRequest = allocated / (double)values.Length,
            Gen0 = gc[0], Gen1 = gc[1], Gen2 = gc[2], GcPauseMilliseconds = paused.TotalMilliseconds,
            CompletedMonitorContentions = contended, SampledPeakWorkingSetBytes = sampledPeak,
            RequestP50Microseconds = Percentile(.50), RequestP95Microseconds = Percentile(.95), RequestP99Microseconds = Percentile(.99),
            Note = "All-thread allocations include identical incoming quote construction and probe/barrier overhead. Working set sampled after each batch. Percentiles are instrumented individual registered-cache Apply+Read calls, including monitor wait and deadline scheduling, and are separate from BenchmarkDotNet batch means."
        };
        requestTicks = null;
        return result;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        stopped = true;
        if (starts is null) return;
        foreach (var start in starts) start.Set();
        foreach (var thread in threads) if (!thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Contention fixture did not stop.");
        foreach (var start in starts) start.Dispose();
        finished.Dispose();
    }

}
