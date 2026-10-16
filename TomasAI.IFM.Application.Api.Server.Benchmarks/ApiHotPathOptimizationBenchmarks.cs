using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Benchmarks.Baselines;
using TomasAI.IFM.Application.Api.Server.Core.Observability.HealthChecks;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Domain.Application.Event;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks;

/// <summary>Before = frozen 51075e3eb code; After = actual production entry points. No network, broker or database access.</summary>
[MemoryDiagnoser, ThreadingDiagnoser, CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 3, iterationCount: 8)]
[GcServer(true)]
public class ApiHotPathOptimizationBenchmarks
{
    const int Workers = 4, ReadsPerWorker = 4096;
    readonly FixedClock clock = new();
    readonly HealthCheckContext healthContext = new();
    readonly ActorSubject subject = new(ActorType.Realtime, "BenchmarkActor", "Tick", "ES");
    readonly Guid generation = Guid.NewGuid();
    readonly Barrier barrier = new(Workers + 1);
    readonly Thread[] readers = new Thread[Workers];
    volatile int work;
    volatile bool stopping;
    BaselineTradeRealtimeHotCache beforeCache = null!;
    TradeRealtimeHotCache afterCache = null!;
    TradeRealtimeSnapshot changedSnapshot = null!;
    BaselineTradeRealtimeCacheRegistry beforeRegistry = null!;
    TradeRealtimeCacheRegistry afterRegistry = null!;
    FuturesOptionTickDataV2ReadModel duplicate = null!;
    TradeEntityId lookupKey;
    readonly BaselineRealtimeActorCompletionEvidence beforeEvidence = new();
    readonly RealtimeActorCompletionEvidence afterEvidence = new();
    BaselineActorRuntimeHealthCheck beforeReadiness = null!;
    ActorRuntimeHealthCheck afterReadiness = null!;
    BaselineApplicationLifecycleHealthCheck beforeLifecycle = null!;
    ApplicationLifecycleHealthCheck afterLifecycle = null!;

    [GlobalSetup]
    public void Setup()
    {
        var trade = CreateTrade(1501);
        lookupKey = trade.Id;
        beforeCache = new(trade, FixedClock.Date, clock); afterCache = new(trade, FixedClock.Date, clock);
        var position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(trade.Id, trade.StrategyKind),
            ValueDate = FixedClock.Date, IsOpen = true, PositionSequence = 1, RouteGeneration = 1,
            Legs = trade.Legs.Select((leg, i) => new StrategyPositionLeg { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId,
                SignedQuantity = leg.SignedQuantity, OpeningPrice = 5 + i, CurrentPrice = 10 + i }).ToArray() };
        beforeCache.Apply(position); afterCache.Apply(position);
        foreach (var leg in trade.Legs) { var observation = Quote(leg.ContractId); beforeCache.Apply(observation); afterCache.Apply(observation); }
        var snapshot = beforeCache.Read();
        RequireEqual(snapshot, afterCache.Read() with { MonitoringGeneration = snapshot.MonitoringGeneration });
        changedSnapshot = snapshot with { Legs = snapshot.Legs.SetItem(0, snapshot.Legs[0] with {
            Observation = snapshot.Legs[0].Observation! with { Delta = -.47, Gamma = .021 } }) };
        RequireEqual(beforeCache.Qualify(changedSnapshot), TradeRealtimeSnapshotCalculations.Qualify(changedSnapshot, clock.GetUtcNow()));
        RequireEqual(BaselineTradeRealtimeHotCache.CalculateSpreads(snapshot), TradeRealtimeSnapshotCalculations.CalculateSpreads(snapshot));
        if (beforeCache.Expire() || afterCache.Expire()) throw new InvalidOperationException("Fresh idle snapshots must not advance revisions.");

        beforeRegistry = new(); afterRegistry = new();
        for (var i = 0; i < 1024; i++)
        {
            var item = CreateTrade(1501 + i, i < 8);
            beforeRegistry.Register(item, FixedClock.Date); afterRegistry.Register(item, FixedClock.Date);
        }
        duplicate = Quote("shared");
        if (beforeRegistry.Apply(duplicate).Length != 8 || afterRegistry.Apply(duplicate).Length != 8)
            throw new InvalidOperationException("The fanout workload must contain eight interested trades.");
        beforeRegistry.Expire(); afterRegistry.Expire(); // Publish/warm resident membership snapshots.

        // Only the real IsReady/SetReadiness leaf is exercised; no host services or actor intake are constructed.
        var supervisor = (ActorSupervisor)RuntimeHelpers.GetUninitializedObject(typeof(ActorSupervisor));
        supervisor.SetReadiness(true);
        var registry = new ActorRegistry(Enumerable.Repeat(typeof(ActorSupervisor), 64).ToArray());
        beforeReadiness = new(supervisor, registry); afterReadiness = new(supervisor, registry);
        var status = new ApplicationStartupStatusStore(); status.Set(new() { State = ApplicationLifecycleState.Running,
            ValueDate = FixedClock.Date, Summary = "Running", ProcessBootId = Guid.NewGuid(), CommandId = Guid.NewGuid() });
        var handoff = new ApplicationStartupHandoffStatusStore(); handoff.Set(new() { State = ApplicationStartupHandoffState.LifecycleObserved,
            AcceptedAtUtc = clock.GetUtcNow().UtcDateTime, ObservedAtUtc = clock.GetUtcNow().UtcDateTime, AttemptCount = 1 });
        beforeLifecycle = new(status, handoff); afterLifecycle = new(status, handoff);
        RequireEqual(beforeReadiness.CheckHealthAsync(healthContext).Result, afterReadiness.CheckHealthAsync(healthContext).Result);
        RequireEqual(beforeLifecycle.CheckHealthAsync(healthContext).Result, afterLifecycle.CheckHealthAsync(healthContext).Result);
        for (var i = 0; i < Workers; i++) { readers[i] = new Thread(ReadWorker) { IsBackground = true }; readers[i].Start(); }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("01_IdleExpiry")]
    public bool IdleExpiryBefore() => beforeCache.Expire();
    [Benchmark, BenchmarkCategory("01_IdleExpiry")]
    public bool IdleExpiryAfter() => afterCache.Expire();
    [Benchmark(Baseline = true), BenchmarkCategory("02_Qualification")]
    public TradeRealtimeSnapshot QualificationBefore() => beforeCache.Qualify(changedSnapshot);
    [Benchmark, BenchmarkCategory("02_Qualification")]
    public TradeRealtimeSnapshot QualificationAfter() => TradeRealtimeSnapshotCalculations.Qualify(changedSnapshot, clock.GetUtcNow());
    [Benchmark(Baseline = true), BenchmarkCategory("03_SpreadMath")]
    public ImmutableArray<TradeSpreadRealtimeSnapshot> SpreadMathBefore() => BaselineTradeRealtimeHotCache.CalculateSpreads(changedSnapshot);
    [Benchmark, BenchmarkCategory("03_SpreadMath")]
    public ImmutableArray<TradeSpreadRealtimeSnapshot> SpreadMathAfter() => TradeRealtimeSnapshotCalculations.CalculateSpreads(changedSnapshot);
    [Benchmark(Baseline = true), BenchmarkCategory("04_LegLookup")]
    public int LegLookupBefore() => Enumerable.Range(0, changedSnapshot.Legs.Length).FirstOrDefault(i => changedSnapshot.Legs[i].Definition.ContractId == "option-1501-3", -1);
    [Benchmark, BenchmarkCategory("04_LegLookup")]
    public int LegLookupAfter() => TradeRealtimeSnapshotCalculations.FindLegIndex(changedSnapshot.Legs, "option-1501-3");
    [Benchmark(Baseline = true), BenchmarkCategory("05_DuplicateFanout")]
    public TradeRealtimeSnapshot[] FanoutBefore() => beforeRegistry.Apply(duplicate);
    [Benchmark, BenchmarkCategory("05_DuplicateFanout")]
    public TradeRealtimeSnapshot[] FanoutAfter() => afterRegistry.Apply(duplicate);
    [Benchmark(Baseline = true, OperationsPerInvoke = Workers * ReadsPerWorker), BenchmarkCategory("06_ConcurrentLookup")]
    public void ConcurrentLookupBefore() => RunWorkers(0);
    [Benchmark(OperationsPerInvoke = Workers * ReadsPerWorker), BenchmarkCategory("06_ConcurrentLookup")]
    public void ConcurrentLookupAfter() => RunWorkers(1);
    [Benchmark(Baseline = true), BenchmarkCategory("07_RegistryExpiry1024")]
    public TradeRealtimeSnapshot[] RegistryExpiryBefore() => beforeRegistry.Expire();
    [Benchmark, BenchmarkCategory("07_RegistryExpiry1024")]
    public TradeRealtimeSnapshot[] RegistryExpiryAfter() => afterRegistry.Expire();
    [Benchmark(Baseline = true, OperationsPerInvoke = Workers * ReadsPerWorker), BenchmarkCategory("08_ConcurrentEvidence")]
    public void EvidenceBefore() => RunWorkers(2);
    [Benchmark(OperationsPerInvoke = Workers * ReadsPerWorker), BenchmarkCategory("08_ConcurrentEvidence")]
    public void EvidenceAfter() => RunWorkers(3);
    [Benchmark(Baseline = true), BenchmarkCategory("09_ReadinessHealth")]
    public Task<HealthCheckResult> ReadinessBefore() => beforeReadiness.CheckHealthAsync(healthContext);
    [Benchmark, BenchmarkCategory("09_ReadinessHealth")]
    public Task<HealthCheckResult> ReadinessAfter() => afterReadiness.CheckHealthAsync(healthContext);
    [Benchmark(Baseline = true), BenchmarkCategory("10_LifecycleHealth")]
    public Task<HealthCheckResult> LifecycleBefore() => beforeLifecycle.CheckHealthAsync(healthContext);
    [Benchmark, BenchmarkCategory("10_LifecycleHealth")]
    public Task<HealthCheckResult> LifecycleAfter() => afterLifecycle.CheckHealthAsync(healthContext);

    void RunWorkers(int selected) { work = selected; barrier.SignalAndWait(); barrier.SignalAndWait(); }
    void ReadWorker()
    {
        while (true)
        {
            barrier.SignalAndWait(); if (stopping) return;
            for (var i = 0; i < ReadsPerWorker; i++)
                switch (work)
                {
                    case 0: GC.KeepAlive(beforeRegistry.Find(lookupKey)); break;
                    case 1: GC.KeepAlive(afterRegistry.Find(lookupKey)); break;
                    case 2: beforeEvidence.Record("GLBX.MDP3", generation, subject); break;
                    default: afterEvidence.Record("GLBX.MDP3", generation, subject); break;
                }
            barrier.SignalAndWait();
        }
    }
    [GlobalCleanup]
    public void Cleanup()
    {
        if (readers[0] is not null) { stopping = true; barrier.SignalAndWait(); foreach (var thread in readers) thread.Join(); }
        barrier.Dispose();
    }
    public void VerifyEquivalence()
    {
        var random = new Random(42);
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            var legs = changedSnapshot.Legs.Select(leg => leg with
            {
                Definition = leg.Definition with { CashMultiplier = random.Next(4) == 0 ? 0 : 50,
                    PutCall = (byte)random.Next(1, 4) },
                Position = random.Next(5) == 0 ? null : leg.Position! with { SignedQuantity = random.Next(-3, 4) },
                Observation = random.Next(5) == 0 ? null : leg.Observation! with {
                    GreeksAvailable = random.Next(3) == 0 ? null : random.Next(2) == 0,
                    ValidUntilUtc = FixedClock.At.AddSeconds(random.Next(-60, 61)),
                    Delta = random.NextDouble(), Gamma = random.NextDouble(), Vega = random.NextDouble(),
                    Theta = random.NextDouble(), Rho = random.NextDouble(),
                    SourceGeneration = random.Next(3) == 0 ? generation : leg.Observation!.SourceGeneration,
                    ObservedAtUtc = FixedClock.At.AddMilliseconds(random.Next(3000)) }
            }).Select(leg => leg.Observation is null ? leg with { QuoteStatus = "Initializing", PricingStatus = "Initializing", Scenario = null } : leg)
                .Take(random.Next(5)).ToImmutableArray();
            var input = changedSnapshot with { Legs = legs };
            RequireEqual(beforeCache.Qualify(input), TradeRealtimeSnapshotCalculations.Qualify(input, clock.GetUtcNow()));
            RequireEqual(BaselineTradeRealtimeHotCache.CalculateSpreads(input), TradeRealtimeSnapshotCalculations.CalculateSpreads(input));
            var quote = Quote(changedSnapshot.Legs[iteration % 4].Definition.ContractId) with {
                TickId = iteration + 2, ObservationRevision = iteration + 2,
                Delta = random.NextDouble(), Gamma = random.NextDouble(), Vega = random.NextDouble(),
                Theta = random.NextDouble(), Rho = random.NextDouble() };
            RequireEqual(beforeCache.Apply(quote), afterCache.Apply(quote));
            var published = beforeCache.Read();
            RequireEqual(published, afterCache.Read() with { MonitoringGeneration = published.MonitoringGeneration });
        }
    }
    internal static EstablishedTradeDefinition CreateTrade(int id, bool shared = false) => new()
    {
        Id = new(101, 701, 1901, id), StrategyKind = TradeStrategyKind.IronCondor,
        Legs = Enumerable.Range(0, 4).Select(i => new TradeLegDefinition { TradeLegId = Guid.NewGuid(),
            ContractId = shared && i == 0 ? "shared" : $"option-{id}-{i}", PutCall = (byte)(i < 2 ? 2 : 1),
            SignedQuantity = i % 2 == 0 ? -2 : 2, Strike = 5000 + i * 10, CashMultiplier = 50 }).ToArray()
    };
    internal static FuturesOptionTickDataV2ReadModel Quote(string contract) => new(contract, FixedClock.Date, 1,
        new(15, 0), 10, 9, 11, 1, 2, .2, 5000, -.3, .01, 12, -3, .1)
    {
        SourceGeneration = new("00000000-0000-0000-0000-000000000001"), ObservationRevision = 1,
        ObservedAtUtc = FixedClock.At, CalculatedAtUtc = FixedClock.At, ValidUntilUtc = FixedClock.At.AddHours(1), GreeksAvailable = true
    };
    static void RequireEqual<T>(T before, T after)
    {
        if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after))
            throw new InvalidOperationException("Before/after benchmark output mismatch.");
    }
    internal sealed class FixedClock : TimeProvider
    {
        internal static readonly DateTimeOffset At = new(2026, 10, 16, 15, 0, 0, TimeSpan.Zero);
        internal static readonly DateOnly Date = new(2026, 10, 16);
        public override DateTimeOffset GetUtcNow() => At;
    }
}
