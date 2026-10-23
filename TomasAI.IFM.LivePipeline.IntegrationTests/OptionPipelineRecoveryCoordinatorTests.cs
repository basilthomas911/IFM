using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class OptionPipelineRecoveryCoordinatorTests
{
    [Fact]
    public async Task Joining_Host_recovery_preserves_its_original_deadline_instead_of_starting_another_two_minutes()
    {
        var clock = new LivePipelineIntegrationTests.ManualTime();
        var owned = clock.GetUtcNow().AddSeconds(20);
        var runtime = new Runtime { Inspection = new(new(2026, 10, 23), "Host recovery", [], owned) };
        var recovery = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance, clock);
        recovery.Observe(default); await runtime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(owned, runtime.Deadline);
        clock.Advance(TimeSpan.FromSeconds(19)); Assert.Null(recovery.Failure);
        clock.Advance(TimeSpan.FromSeconds(1)); await recovery.Completion!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("Failed", recovery.State);
    }

    [Fact]
    public async Task Full_default_deadline_is_119_seconds_quiet_and_120_seconds_terminal()
    {
        var clock = new LivePipelineIntegrationTests.ManualTime();
        var runtime = new Runtime();
        var recovery = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance, clock);
        recovery.Observe(CancellationToken.None);
        await runtime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        clock.Advance(TimeSpan.FromSeconds(119)); Assert.Equal("Recovering", recovery.State); Assert.Null(recovery.Failure);
        clock.Advance(TimeSpan.FromSeconds(1));
        await recovery.Completion!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("Failed", recovery.State); Assert.NotNull(recovery.Failure);
    }

    [Fact]
    public async Task Success_requires_the_runtime_to_finish_reset_and_end_to_end_verification()
    {
        var runtime = new Runtime();
        var recovery = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance);
        Assert.Null(recovery.Observe(CancellationToken.None)); Assert.Equal("Recovering", recovery.State);
        Assert.Null(recovery.Observe(CancellationToken.None));
        await runtime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2)); Assert.Equal(1, runtime.Calls);
        runtime.Complete.SetResult(); await recovery.Completion!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("Healthy", recovery.State); Assert.Null(recovery.Failure);
        Assert.True(runtime.Deadline <= DateTimeOffset.UtcNow.AddMinutes(2));
    }

    [Fact]
    public async Task Noncooperative_reset_becomes_visible_failure_and_cannot_report_late_success()
    {
        var runtime = new Runtime();
        var recovery = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance,
            recoveryBudget: TimeSpan.FromMilliseconds(100));
        recovery.Observe(CancellationToken.None);
        await recovery.Completion!.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("Failed", recovery.State); var failure = recovery.Failure!;
        Assert.Contains("deadline expired", failure.ErrorMessage);
        Assert.Contains("API restart is disabled", failure.ErrorMessage);
        runtime.Complete.SetResult(); await Task.Delay(50);
        Assert.Equal("Failed", recovery.State);
        Assert.Equal(failure.CommandId, recovery.Observe(CancellationToken.None)!.CommandId);
        Assert.Equal(1, runtime.Calls);
    }

    [Fact]
    public async Task Reset_exception_is_terminal_and_market_close_clears_the_latched_failure()
    {
        var runtime = new Runtime(); runtime.Complete.SetException(new InvalidOperationException("Lease restoration failed"));
        var recovery = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance);
        recovery.Observe(CancellationToken.None); await recovery.Completion!.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains("Lease restoration failed", recovery.Failure!.ErrorMessage);
        runtime.Inspection = new(null, null, []);
        Assert.Null(recovery.Observe(CancellationToken.None)); Assert.Equal("Healthy", recovery.State);
    }

    [Fact]
    public async Task Session_close_cancels_recovery_without_publishing_a_failure_for_the_next_session()
    {
        var runtime = new Runtime();
        var recovery = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance);
        recovery.Observe(CancellationToken.None); var work = recovery.Completion!;
        await runtime.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        runtime.Inspection = new(null, null, []); recovery.Observe(CancellationToken.None);
        await work.WaitAsync(TimeSpan.FromSeconds(2)); Assert.Null(recovery.Failure);
        runtime.Complete.SetResult();
    }

    [Fact]
    public void Healthy_or_closed_sessions_never_start_recovery()
    {
        var runtime = new Runtime { Inspection = new(new(2026, 10, 22), null, []) };
        var recovery = new OptionPipelineRecoveryCoordinator(runtime, NullLogger<OptionPipelineRecoveryCoordinator>.Instance);
        recovery.Observe(CancellationToken.None); Assert.Null(recovery.Completion); Assert.Equal(0, runtime.Calls);
    }

    sealed class Runtime : IOptionPipelineRecoveryRuntime
    {
        public OptionPipelineInspection Inspection = new(new(2026, 10, 22), "monitoring stalled", ["trade-a"]);
        public TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Complete = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls; public DateTimeOffset Deadline;
        public OptionPipelineInspection Inspect() => Inspection;
        public Task RecoverAndVerifyAsync(Guid operationId, OptionPipelineInspection inspection, DateTimeOffset deadlineUtc, CancellationToken cancellationToken)
        { Interlocked.Increment(ref Calls); Deadline = deadlineUtc; Entered.TrySetResult(); return Complete.Task; }
    }
}
