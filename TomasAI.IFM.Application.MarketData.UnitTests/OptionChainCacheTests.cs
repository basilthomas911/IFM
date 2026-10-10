using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.OptionChainCache;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;
using Cache = TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionChainCacheTests
{
    const string Policy = "reviewed-policy/v1";
    static readonly DateOnly Date = new(2026, 9, 8);
    sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = At;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    sealed class Source : ICompositionMarketSource
    {
        public Task<CompositionMarketPage> ReadAsync(CompositionSnapshotRequest request, string? continuation, CancellationToken token)
        {
            var context = Context();
            CompositionMarketInstrument row = new(context.Contract.ContractId, Quote(context.Contract.ContractId, 100),
                context, 5000, true, Quote(context.Contract.UnderlyingContractId, 5000));
            return Task.FromResult(new CompositionMarketPage("scope/v1", Generation, 1, [row], null));
        }
    }
    static OptionChainSnapshotRequest Request() => new("fixture-scope", Generation, Date, "Daily", At.AddSeconds(2))
        { ConfigurationDigest = Policy };
    internal static async Task<MarketCompositionSnapshot> Snapshot()
    {
        var provider = new MarketCompositionSnapshotProvider(new Source(), new Clock(At));
        var result = await provider.CaptureAsync(new(Guid.NewGuid(), "fixture-scope", "Daily", Generation, At, At.AddSeconds(2), true), default);
        Assert.Null(result.Failure); return result.Snapshot!;
    }

    [Fact]
    public async Task Ready_read_returns_one_prevalued_version_and_never_calls_a_market_source()
    {
        var snapshot = await Snapshot();
        using var cache = new Cache(new MutableClock());
        cache.Admit(Generation);
        Assert.True(cache.Publish(snapshot, Date, Policy, 7));
        var result = cache.TryGetSnapshot(Request());
        Assert.True(result.IsReady); Assert.Same(snapshot, result.Snapshot); Assert.Equal(7, result.ChainVersion);
        Assert.Equal(1, cache.GetStatus(new("fixture-scope", Generation)).ContractCount);
        // The reader has no provider/repository dependency. Repeated reads retain exactly the same evidence.
        for (var i = 0; i < 1000; i++) Assert.Same(snapshot, cache.TryGetSnapshot(Request()).Snapshot);
    }

    [Fact]
    public async Task Freshness_is_rechecked_without_any_background_refresh()
    {
        var time = new MutableClock(); using var cache = new Cache(time);
        cache.Admit(Generation); cache.Publish(await Snapshot(), Date, Policy, 1);
        time.Now = At.AddMilliseconds(1001);
        Assert.Equal("SnapshotExpired", cache.TryGetSnapshot(Request()).ReasonCode);
        Assert.Equal("SnapshotExpired", cache.GetStatus(new("fixture-scope", Generation)).ReasonCode);
    }

    [Fact]
    public async Task Tighter_request_age_cannot_relax_qualified_context()
    {
        var time = new MutableClock(); using var cache = new Cache(time);
        cache.Admit(Generation); cache.Publish(await Snapshot(), Date, Policy, 1);
        time.Now = At.AddMilliseconds(101);
        Assert.Equal("QuoteStale", cache.TryGetSnapshot(Request() with { MaximumQuoteAgeMilliseconds = 100 }).ReasonCode);
    }

    [Fact]
    public async Task Admission_closure_immediately_fences_quotes_and_stale_producer_cannot_readmit_them()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var identity = new DatasetWorkerAdmission("GLBX.MDP3", Date, Guid.NewGuid(), Generation, 1);
        using var cache = new Cache(new MutableClock(), admissions: admissions);
        admissions.Admit(identity); Assert.True(cache.Publish(await Snapshot(), Date, Policy, 1));
        admissions.Close(identity.Dataset, Generation);
        cache.Admit(Generation);
        Assert.False(cache.Publish(await Snapshot(), Date, Policy, 2));
        Assert.Equal("ScopeNotReady", cache.TryGetSnapshot(Request()).ReasonCode);
        var next = Guid.NewGuid(); admissions.Admit(identity with { GenerationId = next, WorkerInstanceId = Guid.NewGuid() });
        cache.Admit(Generation);
        Assert.False(cache.Publish(await Snapshot(), Date, Policy, 3));
        cache.Fence(Generation);
        Assert.Equal("ScopeNotReady", cache.TryGetSnapshot(Request() with { GenerationId = next }).ReasonCode);
    }

    [Fact]
    public async Task Required_contract_identity_is_bounded_and_missing_coverage_is_explicit()
    {
        using var cache = new Cache(new MutableClock()); cache.Admit(Generation); cache.Publish(await Snapshot(), Date, Policy, 1);
        Assert.True(cache.TryGetSnapshot(Request() with { RequiredContractIds = [Contract().ContractId] }).IsReady);
        Assert.Equal("CoverageIncomplete", cache.TryGetSnapshot(Request() with { RequiredContractIds = ["missing"] }).ReasonCode);
        Assert.Equal(OptionChainSnapshotOutcome.Rejected, cache.TryGetSnapshot(Request() with { RequiredContractIds = ["same", "same"] }).Outcome);
        Assert.Equal(OptionChainSnapshotOutcome.Rejected, cache.TryGetSnapshot(Request() with { RequiredContractIds = default }).Outcome);
        Assert.Equal("UnsupportedFundPortfolioParameters", cache.TryGetSnapshot(Request() with { FundPortfolioParameterSchema = "v1" }).ReasonCode);
    }

    [Fact]
    public async Task Scope_policy_horizon_and_value_date_cannot_cross()
    {
        using var cache = new Cache(new MutableClock()); cache.Admit(Generation); cache.Publish(await Snapshot(), Date, Policy, 1);
        Assert.Equal("PolicyNotReady", cache.TryGetSnapshot(Request() with { ValueDate = Date.AddDays(1) }).ReasonCode);
        Assert.Equal("PolicyNotReady", cache.TryGetSnapshot(Request() with { ConfigurationDigest = "different" }).ReasonCode);
        Assert.Equal("PolicyNotReady", cache.TryGetSnapshot(Request() with { Horizon = "Weekly" }).ReasonCode);
        Assert.Equal("ScopeNotReady", cache.TryGetSnapshot(Request() with { ScopeId = "other" }).ReasonCode);
    }

    sealed record Payload(long Version, ImmutableArray<long> Values);
    [Fact]
    public async Task Concurrent_publication_preserves_atomic_payload_and_monotonic_version()
    {
        var store = new OptionChainSnapshotStore<Payload>(); store.Admit(Generation);
        var writer = Task.Run(() =>
        {
            for (long v = 1; v <= 10000; v++) store.Publish(Generation, "scope", new(v, At, new(v, [v, v, v])));
        });
        for (var i = 0; i < 20000; i++)
        {
            var read = store.Read(Generation, "scope");
            if (read is not null) { Assert.Equal(read.Version, read.Snapshot.Version); Assert.All(read.Snapshot.Values, x => Assert.Equal(read.Version, x)); }
        }
        await writer;
        Assert.Equal(10000, store.Read(Generation, "scope")!.Version);
        Assert.False(store.Publish(Generation, "scope", new(9999, At, new(9999, []))));
        store.Fence(Generation); Assert.Null(store.Read(Generation, "scope"));
    }

    [Fact]
    public void Scope_capacity_cannot_grow_without_bound()
    {
        var store = new OptionChainSnapshotStore<Payload>(1); store.Admit(Generation);
        Assert.True(store.Publish(Generation, "a", new(1, At, new(1, []))));
        Assert.False(store.Publish(Generation, "b", new(2, At, new(2, []))));
        Assert.True(store.Publish(Generation, "a", new(2, At, new(2, []))));
    }
}
