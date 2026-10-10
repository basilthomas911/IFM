using System.Collections.Immutable;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.OptionChainCache;
using Cache = TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache;

namespace TomasAI.IFM.Domain.MarketData.Benchmarks;

/// <summary>Measures resident reads and complete freshness verification. Setup pricing is excluded from read measurements.</summary>
[MemoryDiagnoser, ShortRunJob]
public class StrategyOptionChainCacheBenchmarks
{
    [Params(128, 512, 2048)] public int Contracts { get; set; }
    readonly Guid generation = Guid.NewGuid();
    readonly DateTimeOffset at = new(2026, 9, 8, 16, 0, 0, TimeSpan.Zero);
    OptionChainSnapshotStore<object> store = null!;
    Cache cache = null!;
    OptionChainSnapshotRequest request = null!;
    OptionChainSnapshotRequest missing = null!;
    ImmutableArray<CompositionInstrumentSnapshot> candidates;
    readonly TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.OptionStrategyBiasParameters policy =
        TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterDefaults.IronCondor(Guid.NewGuid(), Guid.NewGuid(), 1).BiasRows[0];
    sealed class Clock(DateTimeOffset at) : TimeProvider { public override DateTimeOffset GetUtcNow() => at; }

    [GlobalSetup]
    public void Setup()
    {
        store = new(); store.Admit(generation); store.Publish(generation, "scope", new(1, at, new object()));
        cache = new(new Clock(at)); cache.Admit(generation);
        var rows = ImmutableArray.CreateBuilder<CompositionInstrumentSnapshot>(Contracts);
        var convention = new OptionPricingConvention
        {
            ContractId = "", Dataset = "GLBX.MDP3", PublisherId = 1, InstrumentId = 10, RawSymbol = "fixture", Root = "ES",
            Exchange = "XCME", Currency = "USD", UnderlyingContractId = "ES-future", ExerciseStyle = OptionExerciseStyle.European,
            SettlementStyle = OptionSettlementStyle.DeliveryOfFuture, ExpirationUtc = at.AddDays(24), LastTradingUtc = at.AddDays(24),
            DayCount = PricingDayCount.Actual365Fixed, CalendarVersion = "fixture-calendar/v1", Multiplier = 50, TickSize = .25m,
            TickRuleVersion = "fixture/v1", DefinitionDigest = new('a', 64), MappingVersion = "fixture/v1", EvidenceId = "benchmark",
            EffectiveFromUtc = at.AddDays(-1), EffectiveUntilUtc = at.AddDays(40)
        };
        var calendar = new OptionPricingCalendar("fixture-calendar/v1", "America/New_York", new(2026, 1, 1), new(2026, 12, 31), new(18, 0), []);
        var curve = new TomasAI.IFM.Framework.MarketData.Contracts.TreasuryCurveSnapshot(new(2026, 9, 8),
            [new(TomasAI.IFM.Framework.MarketData.Contracts.TreasuryTenor.OneMonth, 5m)], at, "FinancialModelingPrep");
        var rate = TreasuryRateConversion.Convert(curve, TomasAI.IFM.Framework.MarketData.Contracts.TreasuryTenor.OneMonth,
            new("FinancialModelingPrep", "benchmark", TreasuryRateConvention.UsTreasuryCmtNominalSemiannual, "fixture/v1", "fixture")).Value!;
        for (var i = 0; i < Contracts; i++)
        {
            var id = "option-" + i;
            var context = new OptionPricingContext(convention with { ContractId = id }, calendar, rate, at.AddHours(1), generation,
                "benchmark", 1000, 250, "fixture/v1");
            var quote = new OptionPricingQuote(id, 99.75m, 100.25m, 10, 10, at, at, 1, generation);
            var underlying = quote with { ContractId = "ES-future", Bid = 4999.75m, Ask = 5000.25m };
            // Prepared synthetic values benchmark only the reader; actual pricing correctness is covered by integration tests.
            rows.Add(new(new(id, quote, context, 5000 + i * 5, i % 2 == 0, underlying), new(.2, i % 2 == 0 ? .16 : -.16, .001, -.1, 10, 1, 100, .065, new string('a', 64))));
        }
        var snapshot = new MarketCompositionSnapshot(1, Guid.NewGuid(), "scope", "scope/v1", "Daily", generation, at, at.AddSeconds(1), rows.MoveToImmutable(), "");
        snapshot = snapshot with { Digest = PricingSemanticHash.Compute(snapshot) };
        candidates = snapshot.Instruments;
        cache.Publish(snapshot, new(2026, 9, 8), "policy", 1);
        request = new("scope", generation, new(2026, 9, 8), "Daily", at.AddSeconds(1)) { ConfigurationDigest = "policy" };
        missing = request with { ScopeId = "absent" };
    }

    [Benchmark(Baseline = true)] public OptionChainPublication<object>? AtomicStoreRead() => store.Read(generation, "scope");
    [Benchmark] public OptionChainSnapshotResult VerifiedSnapshotRead() => cache.TryGetSnapshot(request);
    [Benchmark] public OptionChainSnapshotResult MissingScopeRead() => cache.TryGetSnapshot(missing);
    [Benchmark] public ImmutableArray<CompositionInstrumentSnapshot> CandidateFiltering() =>
        TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCandidatePreparation.Select(candidates, policy, at);
    [GlobalCleanup] public void Cleanup() => cache.Dispose();
}
