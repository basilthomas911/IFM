using BenchmarkDotNet.Attributes;
using TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.Baselines;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

/// <summary>Real actor/scheduler/settlement; contract, cache and Notify transport substituted.</summary>
[MemoryDiagnoser]
public class OptionTradeSettlementBenchmarks
{
    [Params(false, true)] public bool Metrics { get; set; }
    [Params(false, true)] public bool Failed { get; set; }
    OptionTradeDeliveryFixture before = null!, after = null!;
    readonly TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events.FuturesTickTradeDataInsertedEvent input = OptionTradeDeliveryFixture.Input(1);
    [GlobalSetup]
    public async Task Setup()
    {
        before = new(Metrics, context => new LegacyOptionTradeRealtimeActor(context)) { FailLookup = Failed };
        after = new(Metrics) { FailLookup = Failed };
        await before.Start(); await after.Start();
        if (await before.Deliver(input) != !(Failed && Metrics) || await after.Deliver(input) != !Failed)
            throw new InvalidDataException("Benchmark did not reproduce the original and repaired settlement contracts.");
    }
    [IterationSetup] public void Reset() { before.Reset(); after.Reset(); }
    [Benchmark(Baseline = true)] public Task<bool> BeforeMetricsDependent() => before.Deliver(input);
    [Benchmark] public Task<bool> AfterRequiredSettlement() => after.Deliver(input);
    [GlobalCleanup] public async Task Cleanup() { await before.DisposeAsync(); await after.DisposeAsync(); }
}
