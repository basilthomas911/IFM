using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Real actor/scheduler with surrogate Functions; after includes the actual flushed file journal.</summary>
[MemoryDiagnoser]
public class IronCondorPositionReplayBenchmarks
{
    [Params("Position", "Monitoring")] public string Kind { get; set; } = "Position";
    [Params("Fresh", "Replay")] public string Mode { get; set; } = "Fresh";
    IronCondorPositionDeliveryFixture before = null!, after = null!;
    IEvent original = null!;
    int beforeSequence, afterSequence;
    [GlobalSetup]
    public async Task Setup()
    {
        var directory = Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety", "condor-replay-bdn-captures", Guid.NewGuid().ToString("N"));
        before = new(false, context => new LegacyIronCondorReplayActor(context));
        after = new(false, recoveryDirectory: directory);
        original = before.Input(Kind, 1);
        await before.Start(); await after.Start();
        if (!await before.Deliver(original) || !await after.Deliver(original)) throw new InvalidDataException("Setup settlement failed.");
        beforeSequence = afterSequence = 1;
    }
    [IterationSetup] public void ResetObservations() { before.Reset(); after.Reset(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeRecapturedRequests()
        => RequiredDelivery(before, Mode == "Fresh" ? before.Input(Kind, ++beforeSequence) : original);
    [Benchmark] public Task<bool> AfterDurableRequests()
        => RequiredDelivery(after, Mode == "Fresh" ? after.Input(Kind, ++afterSequence) : original);
    static async Task<bool> RequiredDelivery(IronCondorPositionDeliveryFixture fixture, IEvent source)
    {
        if (!await fixture.Deliver(source)) throw new InvalidDataException("Measured request failed settlement.");
        return true;
    }
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
