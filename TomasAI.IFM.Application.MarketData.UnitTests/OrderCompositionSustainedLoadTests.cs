using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento.LastPrice;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed partial class OrderCompositionWorkerTests
{
    /// <summary>Opt-in elapsed load of the real managed consumer/pricer/snapshot with a controlled UTC feed.</summary>
    [CompositionSoakFact]
    public async Task Maximum_scope_sustained_pricing_and_one_hundred_runtime_reconstructions()
    {
        var seconds = int.Parse(Environment.GetEnvironmentVariable("IFM_OCP_LOAD_SECONDS") ?? "1800");
        Assert.InRange(seconds, 10, 1800);
        var path = Environment.GetEnvironmentVariable("IFM_OCP_LOAD_EVIDENCE")!;
        var rows = new List<object>();
        void Record(object row)
        {
            rows.Add(row);
            File.WriteAllText(path, JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
        }
        var calendar = Calendar() with { CoverageFrom = Date, CoverageUntil = new(2026, 10, 2),
            TradingDates = Calendar().TradingDates.Where(x => x >= Date && x <= new DateOnly(2026, 10, 2)).ToImmutableArray() };
        var definitions = Enumerable.Range(0, 512).Select(i => new WorkerOptionDefinition(Context() with
        { Calendar = calendar, Contract = Contract() with { ContractId = $"load-{i}", InstrumentId = (uint)(10 + i), RawSymbol = $"load-{i}" } }, 5000m, true)).ToImmutableArray();
        var resetTimes = new List<double>();
        // Reconstruct actual managed runtime/session/consumer resources; process-kill coverage is separate.
        for (var cycle = 0; cycle < 100; cycle++)
        {
            var timer = Stopwatch.StartNew();
            using var prices = CreatePrices(); using var feed = new ChainFeed();
            await using (var runtime = Runtime(Factory(feed), prices))
            {
                var request = Request() with { Options = definitions };
                var acquired = await runtime.AcquireAsync(request, default);
                Assert.True(acquired.Active, acquired.Failure?.Code);
                for (var i = 0; i < 512; i++) feed.Push(QuoteRecord(1, (uint)(10 + i)));
                await Until(() => prices.GetFuturesOptionReader("load-511", Date).TryGetLastQuoteWithGreeks(out _));
                Assert.False((await runtime.ReleaseAsync(new(request.ScopeId, request.LeaseId, Generation), default)).Active);
            }
            resetTimes.Add(timer.Elapsed.TotalMilliseconds);
            if (cycle % 10 == 9) Record(new { Stage = "RuntimeReconstruction", Cycle = cycle + 1,
                Milliseconds = timer.Elapsed.TotalMilliseconds, Heap = GC.GetTotalMemory(false), Rss = Process.GetCurrentProcess().WorkingSet64 });
        }
        using var store = CreatePrices(); using var source = new ChainFeed(); var clock = new MutableClock();
        await using var active = Runtime(Factory(source), store, clock);
        var initial = Request() with { Options = definitions };
        Assert.True((await active.AcquireAsync(initial, default)).Active);
        var owners = definitions.Chunk(128).Select(chunk => new WorkerOptionChainOwner(Guid.NewGuid(),
            chunk.Select(x => x.Pricing.Contract.ContractId).ToImmutableArray())).ToImmutableArray();
        Assert.True((await active.ReleaseAsync(new(initial.ScopeId, Guid.Empty, Generation,
            new(1, "load", owners, WorkerOptionChainRuntime.PhysicalDigest(definitions))), default)).Active);
        await active.ReleaseAsync(new(initial.ScopeId, initial.LeaseId, Generation), default);
        var snapshotter = new MarketCompositionSnapshotProvider(active, clock);
        var elapsed = Stopwatch.StartNew(); var latencies = new List<double>(); long count = 0; uint sequence = 0;
        var startAlloc = GC.GetTotalAllocatedBytes(true); var startPause = GC.GetTotalPauseDuration();
        var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var nextReport = TimeSpan.Zero;
        while (elapsed.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            var batchStart = elapsed.Elapsed;
            clock.Now = At.Add(elapsed.Elapsed);
            store.TryUpdateQuote(new("ES-future", Date, 4999.75m, 10, 1, 5000.25m, 10, 1, ++sequence, clock.Now, clock.Now));
            for (var i = 0; i < 512; i++) { source.Push(QuoteRecord(sequence, (uint)(10 + i), clock.Now)); count++; }
            await Until(() => store.GetFuturesOptionReader("load-511", Date).TryGetLastQuoteWithGreeks(out var q) && q.Tick.SourceSequence == sequence);
            if (elapsed.Elapsed >= nextReport)
            {
                var timer = Stopwatch.StartNew();
                var capture = await snapshotter.CaptureAsync(new(Guid.NewGuid(), initial.ScopeId, "Daily", Generation, clock.Now, clock.Now.AddSeconds(2), true), default);
                latencies.Add(timer.Elapsed.TotalMilliseconds);
                Assert.Null(capture.Failure); Assert.Equal(512, capture.Snapshot!.Instruments.Length);
                Assert.All(capture.Snapshot.Instruments, x => Assert.NotNull(x.Valuation));
                Record(new { Stage = "SustainedLoad", Seconds = elapsed.Elapsed.TotalSeconds, Quotes = count,
                    AllocatedBytes = GC.GetTotalAllocatedBytes(true) - startAlloc, Heap = GC.GetTotalMemory(false),
                    Collections = Enumerable.Range(0, 3).Select(i => GC.CollectionCount(i) - collections[i]).ToArray(),
                    PauseMilliseconds = (GC.GetTotalPauseDuration() - startPause).TotalMilliseconds,
                    Rss = Process.GetCurrentProcess().WorkingSet64, SnapshotMilliseconds = timer.Elapsed.TotalMilliseconds });
                nextReport = elapsed.Elapsed + TimeSpan.FromSeconds(10);
            }
            var delay = TimeSpan.FromMilliseconds(50) - (elapsed.Elapsed - batchStart);
            if (delay > TimeSpan.Zero) await Task.Delay(delay);
        }
        latencies.Sort(); resetTimes.Sort();
        Record(new { Stage = "Completed", Seconds = elapsed.Elapsed.TotalSeconds, Quotes = count, QuotesPerSecond = count / elapsed.Elapsed.TotalSeconds,
            RuntimeReconstructions = 100, ResetP95Milliseconds = resetTimes[95], ResetP99Milliseconds = resetTimes[99],
            SnapshotP95Milliseconds = latencies[(int)(latencies.Count * .95)], SnapshotP99Milliseconds = latencies[(int)(latencies.Count * .99)],
            AllocatedBytesPerSecond = (GC.GetTotalAllocatedBytes(true) - startAlloc) / elapsed.Elapsed.TotalSeconds });
        Assert.False((await active.ReleaseAsync(new(initial.ScopeId, Guid.Empty, Generation,
            new(2, "load", [], WorkerOptionChainRuntime.PhysicalDigest(definitions))), default)).Active);

        static DatabentoLastPriceStore CreatePrices()
        {
            var result = new DatabentoLastPriceStore(Date, 513); result.RegisterContract("ES-future", AssetTypeId.Futures);
            result.TryUpdateQuote(new("ES-future", Date, 4999.75m, 10, 1, 5000.25m, 10, 1, 1, At, At)); return result;
        }
    }
}

public sealed class CompositionSoakFactAttribute : FactAttribute
{
    public CompositionSoakFactAttribute()
    { if (Environment.GetEnvironmentVariable("IFM_OCP_LOAD") != "1") Skip = "Requires explicit sustained-load opt-in and evidence path."; }
}
