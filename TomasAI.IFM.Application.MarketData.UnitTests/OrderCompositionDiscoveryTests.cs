using NSubstitute;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OrderCompositionDiscoveryTests
{
    [Fact]
    public async Task Complete_scope_is_qualified_priced_and_sent_to_worker_with_canonical_sharing_identity()
    {
        var mappings = Substitute.For<IOptionPricingConventionStore>(); var c = Contract();
        mappings.GetAsync(c.ContractId, c.MappingVersion, Arg.Any<CancellationToken>()).Returns(c);
        var source = new CurveSource(Curve()); var clock = new Clock(At);
        var market = Substitute.For<ICompositionMarketDataApi>();
        market.AcquireAsync("GLBX.MDP3", Arg.Any<WorkerOptionChainRequest>(), Arg.Any<CancellationToken>()).Returns(new WorkerOptionChainResult(true, null));
        await using var discovery = new QualifiedCompositionDiscovery(new(mappings), new OptionPricingContextProvider(new(source, clock)), market, clock);
        var request = Request(c);
        var first = await discovery.AcquireAsync(request, default);
        var second = await discovery.AcquireAsync(request with { LeaseId = Guid.NewGuid() }, default);
        Assert.Null(first.Failure); Assert.Null(second.Failure);
        Assert.Equal(first.Lease!.ScopeId, second.Lease!.ScopeId);
        Assert.NotEqual(first.Lease.LeaseId, second.Lease.LeaseId);
        Assert.Equal(c, Assert.Single(first.Lease.Options).Pricing.Contract);
        Assert.Equal(1, source.Calls);
        await discovery.ReleaseAsync(first.Lease, default);
        await market.Received(1).ReleaseAsync("GLBX.MDP3", new(first.Lease.ScopeId, request.LeaseId, Generation), default);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, true)]
    public async Task Incomplete_or_known_american_only_scope_never_allocates_worker_feed(bool complete, bool empty)
    {
        var c = Contract() with { ExerciseStyle = OptionExerciseStyle.American };
        var mappings = Substitute.For<IOptionPricingConventionStore>();
        mappings.GetAsync(c.ContractId, c.MappingVersion, Arg.Any<CancellationToken>()).Returns(c);
        var market = Substitute.For<ICompositionMarketDataApi>(); var source = new CurveSource(Curve()); var clock = new Clock(At);
        await using var discovery = new QualifiedCompositionDiscovery(new(mappings), new OptionPricingContextProvider(new(source, clock)), market, clock);
        var result = await discovery.AcquireAsync(Request(c) with { ScopeComplete = complete }, default);
        Assert.Equal(empty, result.CompleteEmpty);
        Assert.Equal(!complete, result.Failure is not null);
        Assert.Null(result.Lease); Assert.Equal(0, source.Calls);
        await market.DidNotReceive().AcquireAsync(Arg.Any<string>(), Arg.Any<WorkerOptionChainRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Discovery_waits_for_initial_underlying_quote_without_reallocating_its_identity()
    {
        var c = Contract(); var mappings = Substitute.For<IOptionPricingConventionStore>();
        mappings.GetAsync(c.ContractId, c.MappingVersion, Arg.Any<CancellationToken>()).Returns(c);
        var market = Substitute.For<ICompositionMarketDataApi>(); var clock = new Clock(At);
        market.AcquireAsync("GLBX.MDP3", Arg.Any<WorkerOptionChainRequest>(), Arg.Any<CancellationToken>()).Returns(
            new WorkerOptionChainResult(false, new("UnderlyingQuoteUnavailable", "Quote", "", "await source")), new WorkerOptionChainResult(true, null));
        await using var discovery = new QualifiedCompositionDiscovery(new(mappings),
            new OptionPricingContextProvider(new(new CurveSource(Curve()), clock)), market, clock);
        var result = await discovery.AcquireAsync(Request(c), default);
        Assert.Null(result.Failure);
        await market.Received(2).AcquireAsync("GLBX.MDP3", Arg.Is<WorkerOptionChainRequest>(x => x.LeaseId == result.Lease!.LeaseId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Registered_context_refresh_uses_expected_batch_and_stops_after_lease_expiry()
    {
        var c = Contract(); var mappings = Substitute.For<IOptionPricingConventionStore>();
        mappings.GetAsync(c.ContractId, c.MappingVersion, Arg.Any<CancellationToken>()).Returns(c);
        var market = Substitute.For<ICompositionMarketDataApi>(); var clock = new MutableClock();
        market.AcquireAsync("GLBX.MDP3", Arg.Any<WorkerOptionChainRequest>(), Arg.Any<CancellationToken>()).Returns(new WorkerOptionChainResult(true, null));
        await using var discovery = new QualifiedCompositionDiscovery(new(mappings),
            new OptionPricingContextProvider(new(new CurveSource(Curve()), clock)), market, clock);
        var original = await discovery.AcquireAsync(Request(c), default);
        clock.Now = At.AddSeconds(31);
        await discovery.RefreshRegisteredAsync(default);
        await market.Received(1).AcquireAsync("GLBX.MDP3", Arg.Is<WorkerOptionChainRequest>(x =>
            x.LeaseId == original.Lease!.LeaseId && x.ExpectedContextDigest != null), Arg.Any<CancellationToken>());
        clock.Now = At.AddSeconds(61);
        await discovery.RefreshRegisteredAsync(default);
        await market.Received(2).AcquireAsync("GLBX.MDP3", Arg.Any<WorkerOptionChainRequest>(), Arg.Any<CancellationToken>());
    }

    sealed class MutableClock : TimeProvider
    {
        public DateTimeOffset Now = At;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    static CompositionDiscoveryRequest Request(OptionPricingConvention c) => new(Guid.NewGuid(), Generation,
        new(2026, 9, 8), new(2026, 10, 2), At.AddSeconds(60), [new(c.ContractId, c.MappingVersion, c.DefinitionDigest, new()
        {
            Dataset = c.Dataset, RawSymbol = c.RawSymbol, Ticker = c.Root, Underlying = c.UnderlyingContractId,
            Instrument = new(c.PublisherId, c.InstrumentId), Right = OptionRightSelection.Call,
            StrikePrice = 5000, MaturityDate = new(2026, 10, 2),
            ExpirationTimestampNanoseconds = checked((ulong)(c.ExpirationUtc.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100)
        })], true, Calendar(), Publication(), Conversion);
}
