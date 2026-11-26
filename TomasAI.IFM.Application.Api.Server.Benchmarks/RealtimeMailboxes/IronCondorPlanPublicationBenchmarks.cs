using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Real actor, scheduler, file request capture, publication context/cache and file outbox; surrogate Functions/transport.</summary>
[MemoryDiagnoser]
public class IronCondorPlanPublicationBenchmarks
{
    [Params("Position", "Monitoring")] public string Kind { get; set; } = "Position";
    [Params("Fresh", "Replay")] public string Mode { get; set; } = "Fresh";
    IronCondorPositionDeliveryFixture before = null!, after = null!;
    IEvent source = null!;
    int beforeSequence, afterSequence;
    [GlobalSetup]
    public async Task Setup()
    {
        var root = Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/condor-publication-bdn-captures", Guid.NewGuid().ToString("N"));
        before = new(false, recoveryDirectory: Path.Combine(root, "before"), realPublication: true,
            publicationFactory: (supervisor, cache, requests, clock) => new LegacyIronCondorPublicationContext(supervisor,
                NullLogger<IronCondorTradePositionRealtimeActor>.Instance, realtimeCache: cache, requests: requests, timeProvider: clock));
        after = new(false, recoveryDirectory: Path.Combine(root, "after"), realPublication: true);
        source = before.Input(Kind, 1); await before.Start(); await after.Start();
        if (!await before.Deliver(source) || !await after.Deliver(source)) throw new InvalidDataException("Publication setup failed.");
        beforeSequence = afterSequence = 1;
    }
    [IterationSetup] public void Reset() { before.Reset(); after.Reset(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeCacheDrivenPublication()
        => Deliver(before, Mode == "Fresh" ? before.Input(Kind, ++beforeSequence) : source);
    [Benchmark] public Task<bool> AfterRecoverablePublication()
        => Deliver(after, Mode == "Fresh" ? after.Input(Kind, ++afterSequence) : source);
    static async Task<bool> Deliver(IronCondorPositionDeliveryFixture fixture, IEvent value)
    {
        if (!await fixture.Deliver(value)) throw new InvalidDataException("Measured publication did not settle.");
        return true;
    }
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
