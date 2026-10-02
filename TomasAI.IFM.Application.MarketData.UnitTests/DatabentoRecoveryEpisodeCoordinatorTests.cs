using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoRecoveryEpisodeCoordinatorTests
{
    static DatabentoHardRecoveryRequest Request() =>
        new(Guid.NewGuid(), new DateOnly(2026, 9, 30), Guid.NewGuid(), "Test", "Worker failed");

    [Fact]
    public async Task Concurrent_requests_join_one_episode_and_caller_cancellation_does_not_cancel_it()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var coordinator = new DatabentoRecoveryEpisodeCoordinator(async (request, _) =>
        {
            Interlocked.Increment(ref calls);
            started.SetResult();
            await release.Task;
            return new(request.CorrelationId, Guid.NewGuid(), 1,
                DatabentoHardRecoveryOutcome.DatabentoHealthy, "", "Qualified");
        });
        var firstRequest = Request();
        var first = coordinator.RequestAsync(firstRequest);
        await started.Task;
        using var cancellation = new CancellationTokenSource();
        var cancelledWait = coordinator.RequestAsync(Request(), cancellation.Token);
        var joined = coordinator.RequestAsync(Request());
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelledWait);
        release.SetResult();
        (await first).CorrelationId.Should().Be(firstRequest.CorrelationId);
        (await joined).CorrelationId.Should().Be(firstRequest.CorrelationId);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Unrecoverable_result_closes_future_admission()
    {
        var calls = 0;
        var coordinator = new DatabentoRecoveryEpisodeCoordinator((request, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new DatabentoHardRecoveryResult(request.CorrelationId, Guid.Empty,
                3, DatabentoHardRecoveryOutcome.Unrecoverable, "Qualification", "Exhausted"));
        });
        var first = await coordinator.RequestAsync(Request());
        var later = await coordinator.RequestAsync(Request());
        later.Should().Be(first);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Unexpected_executor_exception_is_contained_as_terminal_result()
    {
        var coordinator = new DatabentoRecoveryEpisodeCoordinator((_, _) =>
            throw new InvalidOperationException("fault"));
        var result = await coordinator.RequestAsync(Request());
        result.Outcome.Should().Be(DatabentoHardRecoveryOutcome.Unrecoverable);
        result.FailedStage.Should().Be("EpisodeBoundary");
        result.Detail.Should().Contain("fault");
    }

    [Fact]
    public async Task Late_request_for_replaced_generation_receives_satisfied_result()
    {
        var calls = 0;
        var coordinator = new DatabentoRecoveryEpisodeCoordinator((request, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new DatabentoHardRecoveryResult(request.CorrelationId,
                Guid.NewGuid(), 1, DatabentoHardRecoveryOutcome.DatabentoHealthy, "", "Qualified"));
        });
        var firstRequest = Request();
        var first = await coordinator.RequestAsync(firstRequest);
        var stale = await coordinator.RequestAsync(firstRequest with { CorrelationId = Guid.NewGuid() });
        Assert.Same(first, stale);
        Assert.Equal(1, calls);
        _ = await coordinator.RequestAsync(Request());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Duplicate_reasons_are_bounded_and_state_transitions_to_healthy()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new DatabentoRecoveryEpisodeCoordinator(async (request, _) =>
        {
            started.SetResult();
            await release.Task;
            return new(request.CorrelationId, Guid.NewGuid(), 1,
                DatabentoHardRecoveryOutcome.DatabentoHealthy, "", "Qualified");
        }, policy: new() { MaximumContributingReasons = 2 });
        var accepted = Request();
        var first = coordinator.RequestAsync(accepted);
        await started.Task;
        Assert.Equal(DatabentoRecoveryEpisodeState.Running, coordinator.State);
        var duplicate = coordinator.RequestAsync(accepted with { CorrelationId = Guid.NewGuid() });
        var other = coordinator.RequestAsync(accepted with { CorrelationId = Guid.NewGuid(), Reason = "Other" });
        var overflow = coordinator.RequestAsync(accepted with { CorrelationId = Guid.NewGuid(), Reason = "Overflow" });
        Assert.Equal(["Test: Worker failed", "Test: Other"], coordinator.ContributingReasons);
        release.SetResult();
        var results = await Task.WhenAll(first, duplicate, other, overflow);
        Assert.All(results, result => Assert.Equal(
            ["Test: Worker failed", "Test: Other"], result.ContributingReasons));
        Assert.Equal(DatabentoRecoveryEpisodeState.Healthy, coordinator.State);
    }

    [Fact]
    public async Task Episode_deadline_is_terminal_even_if_executor_ignores_cancellation()
    {
        var coordinator = new DatabentoRecoveryEpisodeCoordinator(async (_, _) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan);
            throw new InvalidOperationException("Unreachable");
        }, policy: new() { EpisodeTimeout = TimeSpan.FromMilliseconds(50) });
        var result = await coordinator.RequestAsync(Request()).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(DatabentoHardRecoveryOutcome.Unrecoverable, result.Outcome);
        Assert.Equal("EpisodeDeadline", result.FailedStage);
        Assert.Equal(DatabentoRecoveryEpisodeState.Unrecoverable, coordinator.State);
        Assert.Same(result, await coordinator.RequestAsync(Request()));
    }

    [Fact]
    public async Task Host_cancellation_returns_stopping_and_later_requests_do_not_throw()
    {
        using var host = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new DatabentoRecoveryEpisodeCoordinator(async (_, cancellation) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            throw new InvalidOperationException("Unreachable");
        }, host.Token);
        var pending = coordinator.RequestAsync(Request());
        await started.Task;
        host.Cancel();
        Assert.Equal(DatabentoHardRecoveryOutcome.ApplicationStopping, (await pending).Outcome);
        Assert.Equal(DatabentoRecoveryEpisodeState.ApplicationStopping, coordinator.State);
        Assert.Equal(DatabentoHardRecoveryOutcome.ApplicationStopping,
            (await coordinator.RequestAsync(Request())).Outcome);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(3600001, 1)]
    [InlineData(1000, 0)]
    [InlineData(1000, 257)]
    public void Invalid_policy_is_rejected_at_construction(int timeoutMilliseconds, int reasons)
    {
        var policy = new DatabentoRecoveryEpisodePolicy
        {
            EpisodeTimeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
            MaximumContributingReasons = reasons
        };
        Assert.Throws<InvalidOperationException>(() =>
            new DatabentoRecoveryEpisodeCoordinator((_, _) => throw new Exception(), policy: policy));
    }
}
