using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Isolates the cost of adding durable open-state ownership to an ordinary trade update.</summary>
[MemoryDiagnoser]
public class BarCheckpointBenchmarks
{
    RealtimeMailboxSafetyFixture.Handler volatileHandler = null!, durableHandler = null!, batchedHandler = null!;
    FuturesTradeSessionBarCheckpointStore store = null!, batchStore = null!;
    long beforeOrdinal = 2, afterOrdinal = 2, batchOrdinal = 2;
    [Params(1, 64)] public int Count { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        store = new(Path.Combine(Path.GetTempPath(), "IFM-bar-benchmark-" + Guid.NewGuid().ToString("N")));
        volatileHandler = new(); durableHandler = new(store);
        batchStore = new(Path.Combine(Path.GetTempPath(), "IFM-bar-batch-benchmark-" + Guid.NewGuid().ToString("N")));
        batchedHandler = new(batchStore);
        await volatileHandler.Receive(RealtimeMailboxSafetyFixture.Trade(1));
        await volatileHandler.Receive(RealtimeMailboxSafetyFixture.Trade(2));
        await durableHandler.Receive(RealtimeMailboxSafetyFixture.Trade(1));
        await durableHandler.Receive(RealtimeMailboxSafetyFixture.Trade(2));
        await batchedHandler.Receive(RealtimeMailboxSafetyFixture.Trade(1));
        await batchedHandler.Receive(RealtimeMailboxSafetyFixture.Trade(2));
    }

    [Benchmark(Baseline = true)]
    public async Task VolatileBefore()
    {
        for (var n = 0; n < Count; n++) await volatileHandler.Receive(RealtimeMailboxSafetyFixture.Trade(++beforeOrdinal));
    }
    [Benchmark]
    public async Task CheckpointAfter()
    {
        for (var n = 0; n < Count; n++) await durableHandler.Receive(RealtimeMailboxSafetyFixture.Trade(++afterOrdinal));
    }
    [Benchmark]
    public async Task BatchedCheckpointAfter()
    {
        var inputs = new TomasAI.IFM.Shared.EventSourcing.IEvent[Count];
        for (var n = 0; n < Count; n++) inputs[n] = RealtimeMailboxSafetyFixture.Trade(++batchOrdinal);
        await batchedHandler.ReceiveBatch(inputs);
    }
    [GlobalCleanup] public void Cleanup() { store.Dispose(); batchStore.Dispose(); }
}
