using System.Collections.Immutable;
using TomasAI.IFM.Framework.MarketData.Contracts;
using TomasAI.IFM.Framework.MarketData.Pricing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using static TomasAI.IFM.Application.MarketData.UnitTests.OrderCompositionPricingPrerequisiteTests;
using Cache = TomasAI.IFM.Application.MarketData.OptionChainCache.OptionChainCache;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionChainBackgroundUpdaterTests
{
    static readonly DateOnly Date = new(2026, 9, 8);
    internal static CompositionMarketDataPlan Plan()
    {
        var c = Contract();
        OptionDefinitionCandidate option = new(c.ContractId, c.MappingVersion, c.DefinitionDigest, new()
        {
            Dataset = c.Dataset, RawSymbol = c.RawSymbol, Ticker = c.Root, Underlying = c.UnderlyingContractId,
            Instrument = new(c.PublisherId, c.InstrumentId), Right = OptionRightSelection.Call,
            StrikePrice = 5000, MaturityDate = DateOnly.FromDateTime(c.ExpirationUtc.UtcDateTime),
            ExpirationTimestampNanoseconds = checked((ulong)(c.ExpirationUtc.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) * 100)
        });
        return new() { ScopeComplete = true, IncludeOptions = true, ValueDate = Date, MaturityDate = Date.AddDays(24),
            Options = [option], Calendar = Calendar(), Publication = Publication(), Conversion = Conversion };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Background_scope_has_independent_ownership_and_recovery_never_publishes_old_quotes(bool closeDuringCapture)
    {
        var clock = new Clock(At); var market = Substitute.For<ICompositionMarketDataApi>();
        var conventions = Substitute.For<IOptionPricingConventionStore>();
        conventions.GetAsync(Contract().ContractId, Contract().MappingVersion, Arg.Any<CancellationToken>()).Returns(Contract());
        var contexts = Substitute.For<IOptionPricingContextProvider>();
        contexts.PrepareAsync(Arg.Any<OptionPricingConvention>(), Arg.Any<OptionPricingCalendar>(), Arg.Any<TreasuryPublicationPolicy>(),
            Arg.Any<TreasuryRateConversionPolicy>(), Generation, Arg.Any<string>(), At, Arg.Any<CancellationToken>()).Returns(new OptionPricingContextResult(Context(), null));
        market.AcquireAsync("GLBX.MDP3", Arg.Any<WorkerOptionChainRequest>(), Arg.Any<CancellationToken>()).Returns(new WorkerOptionChainResult(true, null));
        market.ReleaseAsync("GLBX.MDP3", Arg.Any<WorkerOptionChainRelease>(), Arg.Any<CancellationToken>()).Returns(new WorkerOptionChainResult(false, null));
        var admissions = new DatasetWorkerAdmissionRegistry();
        admissions.Admit(new("GLBX.MDP3", Date, Guid.NewGuid(), Generation, 1));
        using var cache = new Cache(clock, admissions: admissions);
        var plan = Plan(); var digest = PricingSemanticHash.Compute(plan);
        var source = Substitute.For<IOptionUniverseSource>();
        source.LoadAsync(Date, Arg.Any<CancellationToken>()).Returns(ImmutableArray.Create(new PreparedOptionUniverse("global-owner", digest, plan, ["Daily", "Weekly", "Monthly"])));
        var snapshot = await OptionChainCacheTests.Snapshot();
        market.CaptureAsync("GLBX.MDP3", Arg.Any<CompositionSnapshotRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (closeDuringCapture) admissions.Close("GLBX.MDP3", Generation);
            var request = call.Arg<CompositionSnapshotRequest>();
            return new CompositionSnapshotResult(snapshot with { ScopeId = request.ScopeId }, null);
        });
        await using var discovery = new QualifiedCompositionDiscovery(new(conventions), contexts, market, clock);
        using var updater = new OptionChainBackgroundUpdater(source, discovery, market, admissions, cache,
            NullLogger<OptionChainBackgroundUpdater>.Instance, clock);
        await updater.MaintainAsync(default);
        var request = new OptionChainSnapshotRequest(OptionChainBackgroundUpdater.ScopeId(plan, "Daily"), Generation, Date,
            "Daily", At.AddSeconds(1)) { ConfigurationDigest = digest };
        var result = cache.TryGetSnapshot(request);
        Assert.Equal(!closeDuringCapture, result.IsReady);
        Assert.Single(market.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(ICompositionMarketDataApi.AcquireAsync));
        Assert.Single(market.ReceivedCalls(), x => x.GetMethodInfo().Name == nameof(ICompositionMarketDataApi.CaptureAsync));
        var calls = market.ReceivedCalls().Count();
        for (var i = 0; i < 1000; i++) cache.TryGetSnapshot(request);
        Assert.Equal(calls, market.ReceivedCalls().Count());
        admissions.Close("GLBX.MDP3", Generation);
        await updater.CloseSessionAsync(default);
        await updater.MaintainAsync(default);
        await updater.CloseSessionAsync(default);
        await market.Received(1).ReleaseAsync("GLBX.MDP3", Arg.Any<WorkerOptionChainRelease>(), Arg.Any<CancellationToken>());
        Assert.False(cache.TryGetSnapshot(request).IsReady);
    }

    [Fact]
    public void Coverage_batch_unions_overlapping_policies_but_keeps_expiries_and_underlyings_separate()
    {
        var plan = Plan(); var option = plan.Options.Single();
        var policy = TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterDefaults.IronCondor(Guid.NewGuid(), Guid.NewGuid(), 1);
        var first = new PreparedOptionUniverse("first", "first-policy", plan, ["Monthly"]) { Parameters = policy };
        var second = first with { ScopeId = "second", ConfigurationDigest = "second-policy",
            MarketData = plan with { Options = [option with { ContractId = "other-strike", Definition = option.Definition with { StrikePrice = 5050 } }] } };
        var nextExpiry = first with { ScopeId = "next-expiry", MarketData = plan with { MaturityDate = plan.MaturityDate.AddDays(7) } };
        var otherUnderlying = first with { ScopeId = "other-underlying", MarketData = plan with
            { Options = [option with { ContractId = "other-underlying", Definition = option.Definition with { Underlying = "other-future" } }] } };
        var batch = OptionChainBackgroundUpdater.CoalesceCoverage([first, second, nextExpiry, otherUnderlying]);
        Assert.Equal(2, batch[0].MarketData.Options.Length);
        Assert.Equal(PricingSemanticHash.Compute(batch[0].MarketData), PricingSemanticHash.Compute(batch[1].MarketData));
        Assert.Single(batch[2].MarketData.Options); Assert.Single(batch[3].MarketData.Options);
        Assert.Equal("first-policy", batch[0].ConfigurationDigest); Assert.Equal("second-policy", batch[1].ConfigurationDigest);
        Assert.Same(policy, batch[0].Parameters);
    }

    [Fact]
    public async Task Cached_preparation_freezes_first_evidence_without_provider_calls_or_recapture()
    {
        var clock = new Clock(At); var market = Substitute.For<ICompositionMarketDataApi>();
        var store = Substitute.For<ICompositionPreparationStore>(); CompositionPreparation? saved = null;
        store.ReadAsync(Arg.Any<CompositionPreparationKey>(), Arg.Any<CancellationToken>()).Returns(_ => saved);
        store.CommitAsync(Arg.Any<CompositionPreparation>(), Arg.Any<CancellationToken>()).Returns(call => saved ??= call.Arg<CompositionPreparation>());
        using var cache = new Cache(clock); cache.Admit(Generation);
        var snapshot = await OptionChainCacheTests.Snapshot(); cache.Publish(snapshot, Date, "policy", 1);
        var request = new OptionChainSnapshotRequest("fixture-scope", Generation, Date, "Daily", At.AddSeconds(1)) { ConfigurationDigest = "policy" };
        var service = new CompositionPreparationService(market, store, clock);
        var first = await service.PrepareCachedAsync(OrderCompositionPreparationTests.Key(), cache, request, default);
        Assert.Null(first.Failure); Assert.NotNull(first.Preparation);
        Assert.NotEqual(snapshot.SnapshotId, first.Preparation!.Snapshot.SnapshotId);
        cache.Fence(Generation);
        var duplicate = await service.PrepareCachedAsync(OrderCompositionPreparationTests.Key(), cache, request, default);
        Assert.Equal(first.Preparation, duplicate.Preparation);
        Assert.Empty(market.ReceivedCalls());
        CompositionPreparationService.Validate(first.Preparation);
    }
}
