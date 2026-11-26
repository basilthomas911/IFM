using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

[MemoryDiagnoser]
public class DailyFuturesExitIntentBenchmarks
{
    [Params("Position", "Clock")] public string Kind { get; set; } = "Clock";
    [Params("Fresh", "Later")] public string Mode { get; set; } = "Fresh";
    FuturesPositionDeliveryFixture before = null!, after = null!;
    int beforeSequence, afterSequence;
    [GlobalSetup]
    public async Task Setup()
    {
        var path = Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/daily-exit-intent-bdn-captures", Guid.NewGuid().ToString("N"));
        before = new(false, context => new LegacyDailyFuturesIntentActor(context));
        after = new(false, intentDirectory: path);
        before.Time.Now = after.Time.Now = new(2026,8,25,20,0,0,TimeSpan.Zero);
        await before.Start(); await after.Start(); beforeSequence = afterSequence = 1;
        if (!await before.Deliver(FuturesPositionDeliveryFixture.DailyInput(Kind,1)) || !await after.Deliver(FuturesPositionDeliveryFixture.DailyInput(Kind,1)))
            throw new InvalidDataException("Daily intent setup failed.");
    }
    [IterationSetup] public void Reset() { before.Reset(); after.Reset(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeChangingAuthority() => Deliver(before, ++beforeSequence);
    [Benchmark] public Task<bool> AfterFrozenIntent() => Deliver(after, ++afterSequence);
    async Task<bool> Deliver(FuturesPositionDeliveryFixture fixture, int sequence)
    {
        if (!await fixture.Deliver(FuturesPositionDeliveryFixture.DailyInput(Kind, sequence, Mode == "Fresh" ? sequence + 100L : 7L)))
            throw new InvalidDataException("Measured Daily exit source failed.");
        return true;
    }
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
