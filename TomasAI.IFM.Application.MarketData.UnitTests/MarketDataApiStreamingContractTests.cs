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

}
