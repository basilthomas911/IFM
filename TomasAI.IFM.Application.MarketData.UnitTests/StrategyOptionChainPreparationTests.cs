using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class StrategyOptionChainPreparationTests
{
    [Fact]
    public void Coverage_IV_is_background_only_and_cannot_cross_a_worker_generation()
    {
        var admissions = new TomasAI.IFM.Application.MarketData.Databento.Resiliency.DatasetWorkerAdmissionRegistry();
        var date = new DateOnly(2026, 9, 8); var id = Guid.NewGuid();
        admissions.Admit(new("GLBX.MDP3", date, Guid.NewGuid(), Generation, 1));
        var observations = new OptionChainCoverageObservations(admissions);
        Assert.Equal(.20, observations.Read(id));
        observations.Observe(id, Generation, .35, 1, At); Assert.Equal(.35, observations.Read(id));
        var replacement = Guid.NewGuid(); admissions.Admit(new("GLBX.MDP3", date, Guid.NewGuid(), replacement, 2));
        observations.Observe(id, Generation, .90, 2, At); Assert.Equal(.20, observations.Read(id));
        observations.Observe(id, replacement, .40, 3, At); Assert.Equal(.40, observations.Read(id));
    }

    [Fact]
    public void Planner_keeps_preferred_expiry_and_changes_ownership_identity_when_coverage_expands()
    {
        var plan = OptionChainBackgroundUpdaterTests.Plan(); var date = plan.ValueDate;
        var original = plan.Options.Single();
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(plan.Calendar!.TimeZoneId);
        OptionDefinitionCandidate Row(int dte, decimal strike, string underlying = "ES-future")
        {
            var expiry = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(date.AddDays(dte).ToDateTime(new TimeOnly(16, 0)), timezone));
            return original with { ContractId = $"{underlying}-{dte}-{strike}", Definition = original.Definition with
            { Underlying = underlying, StrikePrice = strike, MaturityDate = date.AddDays(dte),
                ExpirationTimestampNanoseconds = checked((ulong)(expiry.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100) } };
        }
        var definitions = new[] { Row(30, 5000), Row(45, 5000), Row(45, 5500), Row(45, 6000), Row(45, 5000, "other-future") }.ToImmutableArray();
        var policy = StrategyOptionChainParameterDefaults.IronCondor(Guid.NewGuid(), Guid.NewGuid(), 1) with { Enabled = true };
        var planner = new OptionUniversePlanner();
        var first = planner.Plan(policy, date, definitions, true, 5000, .20, plan.Calendar!, plan.Publication!, plan.Conversion!);
        Assert.Equal("", first.ReasonCode); Assert.Equal(date.AddDays(45), first.Universes[0].MarketData.MaturityDate);
        Assert.All(first.Universes, u => Assert.Single(u.MarketData.Options.Select(x => x.Definition.Underlying).Distinct()));
        var expanded = planner.Plan(policy, date, definitions, true, 5000, .40, plan.Calendar!, plan.Publication!, plan.Conversion!);
        Assert.NotEqual(first.Universes[0].ScopeId, expanded.Universes[0].ScopeId);
        Assert.Equal("IncompleteDefinitions", planner.Plan(policy, date, definitions, false, 5000, .20, plan.Calendar!, plan.Publication!, plan.Conversion!).ReasonCode);
        var tooMany = planner.Plan(policy with { MaximumContracts = 4 }, date, definitions, true, 5000, .40, plan.Calendar!, plan.Publication!, plan.Conversion!);
        Assert.Equal("ContractCapacityExceeded", tooMany.ReasonCode); Assert.Empty(tooMany.Universes);
    }

    [Theory]
    [InlineData(false, 30, 45, 45)] [InlineData(true, 5, 10, 5)]
    public void Approved_global_defaults_have_three_directions_and_explicit_units(bool vertical, int min, int max, int preferred)
    {
        var id = Guid.NewGuid(); var strategy = Guid.NewGuid();
        var p = vertical ? StrategyOptionChainParameterDefaults.VerticalSpread(id, strategy, 1)
            : StrategyOptionChainParameterDefaults.IronCondor(id, strategy, 1);
        p.Validate(); Assert.False(p.Enabled); Assert.Equal("Development", p.Environment);
        Assert.All(p.BiasRows, row => { Assert.Equal(min, row.MinimumDte); Assert.Equal(max, row.MaximumDte);
            Assert.Equal(preferred, row.PreferredDte); Assert.Equal(50m, Assert.Single(row.PutWingWidths)); Assert.Equal(50m, Assert.Single(row.CallWingWidths)); });
        Assert.Equal((.16m, .16m), Deltas(p, OptionStrategyMarketBias.Neutral));
        Assert.Equal((.20m, .10m), Deltas(p, OptionStrategyMarketBias.Bullish));
        Assert.Equal((.10m, .20m), Deltas(p, OptionStrategyMarketBias.Bearish));
    }
    static (decimal, decimal) Deltas(StrategyOptionChainParameterSet p, OptionStrategyMarketBias bias)
    { var row = p.BiasRows.Single(x => x.MarketBias == bias); return (row.PutDelta.Target, row.CallDelta.Target); }

    [Fact]
    public async Task Shortlist_preserves_exact_wings_and_never_uses_missing_or_stale_wing()
    {
        var raw = (await OptionChainCacheTests.Snapshot()).Instruments.Single();
        CompositionInstrumentSnapshot Row(string id, decimal strike, double delta, bool isCall) => raw with
        { Instrument = raw.Instrument with { ContractId = id, Strike = strike, IsCall = isCall }, Valuation = raw.Valuation! with { Delta = delta } };
        var policy = StrategyOptionChainParameterDefaults.IronCondor(Guid.NewGuid(), Guid.NewGuid(), 1).BiasRows[0];
        ImmutableArray<CompositionInstrumentSnapshot> rows = [Row("short-call", 5100, .16, true), Row("wing-call", 5150, .10, true),
            Row("short-put", 4900, -.16, false), Row("wing-put", 4850, -.10, false), Row("near-call", 5105, .17, true)];
        var result = OptionChainCandidatePreparation.Select(rows, policy, At);
        Assert.Equal(new[] { "short-call", "short-put", "wing-call", "wing-put" }, result.Select(x => x.Instrument.ContractId));
        Assert.Empty(OptionChainCandidatePreparation.Select(rows, policy, At.AddSeconds(2)));
        var missing = OptionChainCandidatePreparation.Select(rows.Where(x => x.Instrument.ContractId != "wing-put").ToImmutableArray(), policy, At);
        Assert.Equal(new[] { "short-call", "wing-call" }, missing.Select(x => x.Instrument.ContractId));
    }

    [Fact]
    public async Task Lookup_key_is_policy_scoped_and_retains_provider_pricing_plan_for_handoff()
    {
        using var cache = new TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache(new Clock(At)); cache.Admit(Generation);
        var raw = await OptionChainCacheTests.Snapshot(); var date = new DateOnly(2026, 9, 8);
        var key = StrategyOptionChainScope.Key(Guid.NewGuid(), 1, date, "Neutral", "Daily");
        Assert.True(cache.Publish(raw, date, "exact-policy", 1, key));
        var read = cache.TryGetSnapshot(new(key, Guid.Empty, date, "Daily", At.AddSeconds(1)) { ConfigurationDigest = "exact-policy" });
        Assert.True(read.IsReady); Assert.Equal("fixture-scope", read.Snapshot!.ScopeId); Assert.Equal(Generation, read.Snapshot.GenerationId);
        Assert.False(cache.TryGetSnapshot(new(key, Guid.NewGuid(), date, "Daily", At.AddSeconds(1)) { ConfigurationDigest = "exact-policy" }).IsReady);
        cache.Fence(Generation); Assert.False(cache.TryGetSnapshot(new(key, Guid.Empty, date, "Daily", At.AddSeconds(1)) { ConfigurationDigest = "exact-policy" }).IsReady);
    }
}
