using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataApiStreamingContractTests
{
    [Fact]
    public async Task FuturesActivationHasDeterministicTrueFalseSemantics()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        (await context.Api.StartStreamingFuturesTickDataAsync(MarketDataApiTestContext.FutureId))
            .Should().BeTrue();
        (await context.Api.StartStreamingFuturesTickDataAsync(MarketDataApiTestContext.FutureId))
            .Should().BeFalse();
        (await context.Api.StopStreamingFuturesTickDataAsync(MarketDataApiTestContext.FutureId))
            .Should().BeTrue();
        (await context.Api.StopStreamingFuturesTickDataAsync(MarketDataApiTestContext.FutureId))
            .Should().BeFalse();

        context.Epoch.TickAggregation.ServiceRunning.Should().BeTrue(
            "live delivery must not control durable futures aggregation");
    }

    [Fact]
    public async Task ConcurrentPerContractActivationHasOneWinner()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            context.Api.StartStreamingFuturesTickDataAsync(MarketDataApiTestContext.FutureId)));

        results.Count(changed => changed).Should().Be(1);
        results.Count(changed => !changed).Should().Be(31);
    }

    [Fact]
    public async Task ConcurrentOptionActivationHasOneWinner()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ =>
            context.Api.StartStreamingFuturesOptionTickDataAsync(MarketDataApiTestContext.CallId)));

        results.Count(changed => changed).Should().Be(1);
        results.Count(changed => !changed).Should().Be(31);
    }

    [Fact]
    public async Task IndividualOptionRejectsStoppedAggregationBeforeRouteAllocation()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        context.Epoch.TickAggregation.ServiceRunning = false;

        var action = () => context.Api.StartStreamingFuturesOptionTickDataAsync(
            MarketDataApiTestContext.CallId);

        await action.Should().ThrowAsync<TickAggregationNotRunningException>();
        context.Epoch.OptionRoutes.IsOwned(MarketDataApiTestContext.CallId)
            .Should().BeFalse();
    }

    [Fact]
    public async Task IndividualOptionRejectsMissingUnderlyingBeforeRouteAllocation()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        context.Epoch.TickAggregation.RunningTickers.Clear();

        var action = () => context.Api.StartStreamingFuturesOptionTickDataAsync(
            MarketDataApiTestContext.CallId);

        await action.Should().ThrowAsync<UnderlyingTickerNotRunningException>();
        context.Epoch.OptionRoutes.IsOwned(MarketDataApiTestContext.CallId)
            .Should().BeFalse();
    }

    [Fact]
    public async Task ChainRejectsStoppedAggregationBeforeTreasuryOrRouteAllocation()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        context.Epoch.TickAggregation.ServiceRunning = false;

        var action = () => context.Api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity,
            [MarketDataApiTestContext.CallId]);

        await action.Should().ThrowAsync<TickAggregationNotRunningException>();
        context.Epoch.TreasuryCurve.QueryCount.Should().Be(0);

        context.Epoch.TickAggregation.ServiceRunning = true;
        (await context.Api.StartStreamingFuturesOptionTickDataAsync(
            MarketDataApiTestContext.CallId)).Should().BeTrue(
            "the rejected chain must not retain route ownership");
    }

    [Fact]
    public async Task ChainRejectsMissingOrStoppedUnderlyingTickerBeforeTreasuryLookup()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        context.Epoch.TickAggregation.RunningTickers.Clear();

        var action = () => context.Api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity,
            [MarketDataApiTestContext.CallId]);

        await action.Should().ThrowAsync<UnderlyingTickerNotRunningException>();
        context.Epoch.TreasuryCurve.QueryCount.Should().Be(0);
    }

    [Fact]
    public async Task IndividualRouteBlocksOverlappingChainRoute()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        await context.Api.StartStreamingFuturesOptionTickDataAsync(MarketDataApiTestContext.CallId);

        var action = () => context.Api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity,
            [MarketDataApiTestContext.CallId, MarketDataApiTestContext.PutId]);

        var exception = await action.Should().ThrowAsync<MarketDataRouteConflictException>();
        exception.Which.ContractId.Should().Be(MarketDataApiTestContext.CallId);
        exception.Which.ExistingOwner.Should().Be("individual");
    }

    [Fact]
    public async Task ChainRouteBlocksOverlappingIndividualRoute()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        await context.Api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity,
            [MarketDataApiTestContext.CallId, MarketDataApiTestContext.PutId]);

        var action = () => context.Api.StartStreamingFuturesOptionTickDataAsync(
            MarketDataApiTestContext.CallId);

        await action.Should().ThrowAsync<MarketDataRouteConflictException>();
    }

    [Fact]
    public async Task IdenticalChainIsIdempotentButDifferentSelectionConflicts()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        (await context.Api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity,
            [MarketDataApiTestContext.CallId, MarketDataApiTestContext.PutId]))
            .Should().BeTrue();
        (await context.Api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity,
            [MarketDataApiTestContext.PutId, MarketDataApiTestContext.CallId]))
            .Should().BeFalse();

        var action = () => context.Api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity,
            [MarketDataApiTestContext.CallId]);
        await action.Should().ThrowAsync<OptionChainConflictException>();
    }

    [Fact]
    public async Task ChainStopReleasesEveryOptionRoute()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        await context.Api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity,
            [MarketDataApiTestContext.CallId, MarketDataApiTestContext.PutId]);

        (await context.Api.StopStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity)).Should().BeTrue();
        (await context.Api.StopStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId,
            MarketDataApiTestContext.OptionMaturity)).Should().BeFalse();
        (await context.Api.StartStreamingFuturesOptionTickDataAsync(
            MarketDataApiTestContext.CallId)).Should().BeTrue();
        (await context.Api.StartStreamingFuturesOptionTickDataAsync(
            MarketDataApiTestContext.PutId)).Should().BeTrue();
    }
}
