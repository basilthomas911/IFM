using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Same synthetic windows: legacy volatile compute versus durable first result and completed redelivery.</summary>
[MemoryDiagnoser]
public class TdiReceiptBenchmarks
{
    readonly LegacyTdiSignalRealtimeState legacy = new();
    FuturesTdiResultReceipts store = null!;
    TdiReceiptFixture.Scenario durable = null!;
    TdiReceiptFixture.Scenario legacyIo = null!;
    LegacyTdiSignalRealtimeProjector legacyProjector = null!;
    TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events.FuturesRsiSignalsGeneratedEvent replay = null!;
    int next = 1;
    [GlobalSetup]
    public async Task Setup()
    {
        store = new(Path.Combine(Path.GetTempPath(), "IFM-tdi-benchmark-" + Guid.NewGuid().ToString("N")));
        durable = new(store); await durable.Start(); replay = TdiReceiptFixture.Source();
        await durable.Process(replay);
        legacyIo = new(new());
        legacyProjector = new(legacyIo.Factory, NullLogger<LegacyTdiSignalRealtimeProjector>.Instance);
        await legacyProjector.StartAsync(legacyIo.Context);
    }
    [Benchmark(Baseline = true)] public async Task LegacyVolatileBefore()
    {
        var source = TdiReceiptFixture.Source(++next);
        legacy.TryEvaluate(source, source.FuturesRsiSignals, FuturesTdiConfiguration.Standard, out var result);
        if (!await legacyProjector.ProcessRealtimeEventAsync(result.Generated)) throw new IOException("Legacy projection failed.");
        legacy.Confirm(result);
    }
    [Benchmark] public async Task DurableNewWindowAfter() => await durable.Process(TdiReceiptFixture.Source(++next));
    [Benchmark] public async Task CompletedReplayAfter() => await durable.Process(TdiReceiptFixture.Source());
    [GlobalCleanup] public void Cleanup() => store.Dispose();
}
