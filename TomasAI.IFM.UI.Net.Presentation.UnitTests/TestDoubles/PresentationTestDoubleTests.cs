using FluentAssertions;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;

public class PresentationTestDoubleTests
{
    [Fact]
    public async Task Dispatcher_ExecutesWorkAndReturnsResult()
    {
        var dispatcher = new TestUiDispatcher();
        var actionExecuted = false;

        await dispatcher.InvokeAsync(() => actionExecuted = true);
        var result = await dispatcher.InvokeAsync(() => 42);

        actionExecuted.Should().BeTrue();
        result.Should().Be(42);
        dispatcher.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task Dispatcher_PropagatesCancellationAndActionFailure()
    {
        var dispatcher = new TestUiDispatcher();
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await dispatcher.InvokeAsync(static () => { }, cancellationSource.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await dispatcher.InvokeAsync(static () =>
                throw new InvalidOperationException("Expected test failure.")));
    }

    [Fact]
    public async Task ManualTimeProvider_AdvanceCompletesVirtualDelay()
    {
        var provider = new ManualTimeProvider(
            new DateTimeOffset(2026, 8, 11, 9, 0, 0, TimeSpan.Zero));
        var delay = Task.Delay(TimeSpan.FromMinutes(5), provider);

        provider.Advance(TimeSpan.FromMinutes(4));
        delay.IsCompleted.Should().BeFalse();

        provider.Advance(TimeSpan.FromMinutes(1));
        await delay.WaitAsync(TimeSpan.FromSeconds(1));
        provider.GetUtcNow().Should().Be(
            new DateTimeOffset(2026, 8, 11, 9, 5, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ManualTimeProvider_AdvanceFiresPeriodicTimersDeterministically()
    {
        var provider = new ManualTimeProvider(DateTimeOffset.UnixEpoch);
        var ticks = 0;
        using var timer = provider.CreateTimer(
            _ => ticks++,
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));

        provider.Advance(TimeSpan.FromMilliseconds(3500));

        ticks.Should().Be(3);
    }

    [Fact]
    public async Task ControlledEventSource_PreservesAwaitedPublicationOrderAndStops()
    {
        var observed = new List<int>();
        await using var source = new ControlledEventSource<int>();
        await source.StartAsync((value, _) =>
        {
            observed.Add(value);
            return ValueTask.CompletedTask;
        });

        await source.PublishAsync(1);
        await source.PublishAsync(2);
        await source.PublishAsync(3);
        await source.StopAsync();

        observed.Should().Equal(1, 2, 3);
        source.IsRunning.Should().BeFalse();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await source.PublishAsync(4));
    }
}
