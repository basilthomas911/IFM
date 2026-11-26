using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class BufferedEodBenchmarks
{
    [Params(1, 64)] public int Count { get; set; }
    BufferedEodFixture fixture = null!;
    LegacyBufferedEodProjector legacy = null!;
    FuturesEodDataInsertedEvent[] inputs = [];
    [IterationSetup]
    public void Setup()
    {
        fixture = new();
        legacy = new(fixture.Factory, fixture.Sequences, NullLogger<LegacyBufferedEodProjector>.Instance, fixture.Configuration);
        inputs = Enumerable.Range(1, Count).Select(i => BufferedEodFixture.Source(i, "ES-BDN-" + fixture.Directory.GetHashCode())).ToArray();
    }
    [Benchmark(Baseline = true)]
    public async Task BeforeCapturePublishAndDrain()
    {
        await legacy.StartAsync(fixture.Context);
        foreach (var input in inputs) await legacy.ProcessRealtimeEventAsync(input);
        await legacy.StopAsync();
        Verify();
    }
    [Benchmark]
    public async Task AfterDurableStagesAndDrain()
    {
        await fixture.Start();
        foreach (var input in inputs) await fixture.Projector.ProcessRealtimeEventAsync(input);
        await fixture.Projector.StopAsync();
        Verify();
    }
    void Verify()
    {
        if (fixture.HistoryIds.Count != Count || fixture.Sent.Count != Count * 2 || fixture.Errors.Count != 0) throw new InvalidOperationException("Benchmark work did not fully drain.");
    }
    [IterationCleanup]
    public void Cleanup()
    {
        legacy.DisposeAsync().AsTask().GetAwaiter().GetResult();
        fixture.DisposeAsync().AsTask().GetAwaiter().GetResult();
        var full = Path.GetFullPath(fixture.Directory);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        if (!full.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("IFM-buffered-eod-", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe benchmark cleanup path.");
        Directory.Delete(full, true);
    }
}
