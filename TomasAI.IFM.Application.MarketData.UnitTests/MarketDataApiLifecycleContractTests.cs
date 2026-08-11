using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class MarketDataApiLifecycleContractTests
{
    [Fact]
    public async Task ConcurrentSameDateStartsCreateOneEpoch()
    {
        var context = new MarketDataApiTestContext();
        context.EpochFactory.BlockStartAtStage = "OptionFeed";

        var starts = Enumerable.Range(0, 8)
            .Select(_ => context.Api.StartAsync(MarketDataApiTestContext.ValueDate))
            .ToArray();
        await context.EpochFactory.StartEntered.Task;
        context.EpochFactory.CreateCount.Should().Be(1);

        context.EpochFactory.ReleaseStart.TrySetResult();
        await Task.WhenAll(starts);

        context.EpochFactory.CreateCount.Should().Be(1);
        context.Api.ActiveValueDate.Should().NotBeNull();
    }

    [Fact]
    public async Task DifferentDateStartRequiresExplicitStop()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var action = () => context.Api.StartAsync(MarketDataApiTestContext.NextValueDate);

        var exception = await action.Should().ThrowAsync<MarketDataApiAlreadyRunningException>();
        exception.Which.ActiveValueDate.Should().Be(MarketDataApiTestContext.ValueDate);
        exception.Which.RequestedValueDate.Should().Be(MarketDataApiTestContext.NextValueDate);
    }

    [Fact]
    public async Task CancellationDuringStartRollsBackAndDisposesAllocatedEpoch()
    {
        var context = new MarketDataApiTestContext();
        context.EpochFactory.BlockStartAtStage = "FuturesFeed";
        using var cancellation = new CancellationTokenSource();

        var start = context.Api.StartAsync(
            MarketDataApiTestContext.ValueDate,
            cancellationToken: cancellation.Token);
        await context.EpochFactory.StartEntered.Task;
        cancellation.Cancel();

        Func<Task> action = async () => await start;
        await action.Should().ThrowAsync<OperationCanceledException>();
        var failedEpoch = context.EpochFactory.Epochs.Single();
        failedEpoch.StopCount.Should().Be(1);
        failedEpoch.DisposeCount.Should().Be(1);
        context.Api.ActiveValueDate.Should().BeNull();
    }

    [Fact]
    public async Task StartFailureCleansCompletedStagesInReverseOrder()
    {
        var context = new MarketDataApiTestContext();
        context.EpochFactory.FailStartAtStage = "TickAggregation";

        var action = () => context.Api.StartAsync(MarketDataApiTestContext.ValueDate);
        await action.Should().ThrowAsync<InvalidOperationException>();

        context.EpochFactory.LifecycleLog.Should().ContainInOrder(
            "Start:ProviderOperations",
            "Start:Publishers",
            "Start:FuturesFeed",
            "Start:TickAggregation",
            "Stop:FuturesFeed",
            "Stop:Publishers",
            "Stop:ProviderOperations",
            $"Dispose:{MarketDataApiTestContext.ValueDate:yyyy-MM-dd}");
    }

    [Fact]
    public async Task StopDuringStartWaitsThenDrainsTheCreatedEpoch()
    {
        var context = new MarketDataApiTestContext();
        context.EpochFactory.BlockStartAtStage = "OptionFeed";

        var start = context.Api.StartAsync(MarketDataApiTestContext.ValueDate);
        await context.EpochFactory.StartEntered.Task;
        var stop = context.Api.StopAsync(MarketDataApiTestContext.ValueDate);

        context.EpochFactory.Epochs.Single().StopCount.Should().Be(0);
        context.EpochFactory.ReleaseStart.TrySetResult();
        await Task.WhenAll(start, stop);

        var epoch = context.EpochFactory.Epochs.Single();
        epoch.StopCount.Should().Be(1);
        epoch.DisposeCount.Should().Be(1);
        context.Api.ActiveValueDate.Should().BeNull();
    }

    [Fact]
    public async Task MismatchedStopDoesNotStopActiveEpoch()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();

        var action = () => context.Api.StopAsync(MarketDataApiTestContext.NextValueDate);

        await action.Should().ThrowAsync<MarketDataApiValueDateMismatchException>();
        context.Api.ActiveValueDate.Should().NotBeNull();
        context.Epoch.StopCount.Should().Be(0);
    }

    [Fact]
    public async Task CompletedStopAllowsNextDateRestart()
    {
        var context = new MarketDataApiTestContext();
        await context.StartAsync();
        await context.Api.StopAsync(MarketDataApiTestContext.ValueDate);

        await context.Api.StartAsync(MarketDataApiTestContext.NextValueDate);

        context.Api.ActiveValueDate.Should().Be(MarketDataApiTestContext.NextValueDate);
        context.EpochFactory.CreateCount.Should().Be(2);
    }

    [Fact]
    public async Task StopIsIdempotentWhenAlreadyStopped()
    {
        var context = new MarketDataApiTestContext();

        await context.Api.StopAsync(MarketDataApiTestContext.ValueDate);
        await context.Api.StopAsync(MarketDataApiTestContext.ValueDate);

        context.EpochFactory.CreateCount.Should().Be(0);
    }
}
