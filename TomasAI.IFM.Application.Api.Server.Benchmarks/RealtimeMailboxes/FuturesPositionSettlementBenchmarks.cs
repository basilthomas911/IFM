using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class FuturesPositionSettlementBenchmarks
{
    [Params(false, true)] public bool Failed { get; set; }
    [Params("Position", "Clock")] public string Kind { get; set; } = "Position";
    FuturesPositionDeliveryFixture before = null!, after = null!;
    [GlobalSetup]
    public async Task Setup()
    {
        before = new(false, context => new LegacyFuturesPositionRealtimeActor(context)) { Failure = Failed ? "throw" : "none", RequiresExit = true };
        after = new(false) { Failure = Failed ? "throw" : "none", RequiresExit = true };
        await before.Start(); await after.Start();
        if (!await before.Deliver(FuturesPositionDeliveryFixture.Input(Kind, 1)) || await after.Deliver(FuturesPositionDeliveryFixture.Input(Kind, 1)) != !Failed)
            throw new InvalidDataException("Expected original/repaired settlement not reproduced.");
    }
    [IterationSetup] public void Reset() { before.Reset(); after.Reset(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeMetricsDependent() => before.Deliver(FuturesPositionDeliveryFixture.Input(Kind, 1));
    [Benchmark] public Task<bool> AfterRequiredSettlement() => after.Deliver(FuturesPositionDeliveryFixture.Input(Kind, 1));
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
