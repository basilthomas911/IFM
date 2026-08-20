using FluentAssertions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Lifecycle;

public class AsyncLifecycleCoordinatorTests
{
    [Fact]
    public async Task InitializeAsync_RepeatedCall_DoesNotInitializeTwice()
    {
        var initializeCount = 0;
        await using var lifecycle = new AsyncLifecycleCoordinator(
            _ =>
            {
                initializeCount++;
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask);

        await lifecycle.InitializeAsync(CancellationToken.None);
        await lifecycle.InitializeAsync(CancellationToken.None);

        initializeCount.Should().Be(1);
        lifecycle.IsRunning.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_CancelsAndAwaitsOwnedWorkBeforeCleanup()
    {
        var backgroundExited = false;
        var cleanupObservedExit = false;
        AsyncLifecycleCoordinator? lifecycle = null;
        lifecycle = new AsyncLifecycleCoordinator(
            _ =>
            {
                lifecycle!.RunAsync(async lifetimeToken =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, lifetimeToken);
                    }
                    finally
                    {
                        backgroundExited = true;
                    }
                });
                return Task.CompletedTask;
            },
            _ =>
            {
                cleanupObservedExit = backgroundExited;
                return Task.CompletedTask;
            });

        await lifecycle.InitializeAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);

        backgroundExited.Should().BeTrue();
        cleanupObservedExit.Should().BeTrue();
        lifecycle.IsRunning.Should().BeFalse();
        await lifecycle.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_RepeatedCall_DoesNotStopTwice()
    {
        var stopCount = 0;
        await using var lifecycle = new AsyncLifecycleCoordinator(
            _ => Task.CompletedTask,
            _ =>
            {
                stopCount++;
                return Task.CompletedTask;
            });

        await lifecycle.InitializeAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);
        await lifecycle.StopAsync(CancellationToken.None);

        stopCount.Should().Be(1);
    }

    [Fact]
    public async Task StopAsync_WhileInitializing_CancelsStartupBeforeWaitingForLifecycleGate()
    {
        var initializationEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupCount = 0;
        await using var lifecycle = new AsyncLifecycleCoordinator(
            async cancellationToken =>
            {
                initializationEntered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            },
            _ =>
            {
                cleanupCount++;
                return Task.CompletedTask;
            });

        var initialization = lifecycle.InitializeAsync(CancellationToken.None);
        await initializationEntered.Task;

        await lifecycle.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(2));

        var observeInitialization = async () => await initialization;
        await observeInitialization.Should().ThrowAsync<OperationCanceledException>();
        cleanupCount.Should().Be(1);
        lifecycle.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_Failure_CancelsOwnedWorkAndCanBeRetried()
    {
        var initializeCount = 0;
        var stopCount = 0;
        AsyncLifecycleCoordinator? lifecycle = null;
        lifecycle = new AsyncLifecycleCoordinator(
            _ =>
            {
                initializeCount++;
                lifecycle!.RunAsync(token => Task.Delay(Timeout.InfiniteTimeSpan, token));
                if (initializeCount == 1)
                    throw new InvalidOperationException("Initialization failed.");
                return Task.CompletedTask;
            },
            _ =>
            {
                stopCount++;
                return Task.CompletedTask;
            });

        var firstAttempt = () => lifecycle.InitializeAsync(CancellationToken.None);
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();

        await lifecycle.InitializeAsync(CancellationToken.None);
        lifecycle.IsRunning.Should().BeTrue();
        stopCount.Should().Be(1, "a failed partial initialization must release resources before retrying");
        await lifecycle.DisposeAsync();
    }
}
