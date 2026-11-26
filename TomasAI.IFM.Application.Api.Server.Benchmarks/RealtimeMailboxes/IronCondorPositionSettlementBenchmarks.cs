using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class IronCondorPositionSettlementBenchmarks
{
    [Params("Position", "Monitoring")] public string Kind { get; set; } = "Position";
    [Params(false, true)] public bool Failed { get; set; }
    IronCondorPositionDeliveryFixture before = null!, after = null!;
    IEvent source = null!;
    [GlobalSetup]
    public async Task Setup()
    {
        before = new(false, context => new LegacyIronCondorPositionRealtimeActor(context)) { Failure = Failed ? "throw" : "none" };
        after = new(false) { Failure = Failed ? "throw" : "none" }; source = before.Input(Kind, 1);
        await before.Start(); await after.Start();
        if (!await before.Deliver(source) || await after.Deliver(source) != !Failed) throw new InvalidDataException("Original/repaired position settlement was not reproduced.");
    }
    [IterationSetup] public void Reset() { before.Reset(); after.Reset(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeMetricsDependent() => before.Deliver(source);
    [Benchmark] public Task<bool> AfterRequiredSettlement() => after.Deliver(source);
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
