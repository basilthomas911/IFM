using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class VerticalSpreadPositionSettlementBenchmarks
{
    [Params(false, true)] public bool Failed { get; set; }
    [Params(false, true)] public bool RequiresExit { get; set; }
    VerticalSpreadPositionDeliveryFixture before = null!, after = null!;
    [GlobalSetup]
    public async Task Setup()
    {
        before = new(false, context => new LegacyVerticalSpreadPositionRealtimeActor(context)) { Failure = Failed ? "throw" : "none", RequiresExit = RequiresExit };
        after = new(false) { Failure = Failed ? "throw" : "none", RequiresExit = RequiresExit };
        await before.Start(); await after.Start();
        if (!await before.Deliver(VerticalSpreadPositionDeliveryFixture.Input(1)) || await after.Deliver(VerticalSpreadPositionDeliveryFixture.Input(1)) != !Failed)
            throw new InvalidDataException("Expected original/repaired settlement not reproduced.");
    }
    [IterationSetup] public void Reset() { before.Reset(); after.Reset(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeMetricsDependent() => before.Deliver(VerticalSpreadPositionDeliveryFixture.Input(1));
    [Benchmark] public Task<bool> AfterRequiredSettlement() => after.Deliver(VerticalSpreadPositionDeliveryFixture.Input(1));
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
