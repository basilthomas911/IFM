using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoHardRecoveryEngineTests
{
    static readonly DatabentoHardRecoveryPolicy NoDelay = new()
    {
        AttemptTwoDelay = TimeSpan.Zero,
        AttemptThreeDelay = TimeSpan.Zero
    };

    [Fact]
    public async Task Three_safe_failed_attempts_produce_one_terminal_result()
    {
        var calls = new List<int>();
        var engine = new DatabentoHardRecoveryEngine((number, _) =>
        {
            calls.Add(number);
            return Task.FromResult(new DatabentoHardAttemptResult(false, true,
                "LocalQualification", new TimeoutException("No records"), []));
        }, TimeProvider.System, NoDelay);
        var request = Request();

        var result = await engine.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.Unrecoverable, result.Outcome);
        Assert.Equal(3, result.Attempts);
        Assert.Equal([1, 2, 3], calls);
        Assert.Equal(3, result.AttemptEvidence.Count);
    }

    [Fact]
    public async Task Unsafe_isolation_failure_stops_before_another_attempt()
    {
        var calls = 0;
        var engine = new DatabentoHardRecoveryEngine((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new DatabentoHardAttemptResult(false, false,
                "ContainOldWorkers", new TimeoutException("Cannot prove process exit"), []));
        }, TimeProvider.System, NoDelay);

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(DatabentoHardRecoveryOutcome.Unrecoverable, result.Outcome);
    }

    [Fact]
    public async Task Second_attempt_success_resets_episode_without_third_attempt()
    {
        var calls = 0;
        var generation = Guid.NewGuid();
        var engine = new DatabentoHardRecoveryEngine((number, _) =>
        {
            calls++;
            return Task.FromResult(number == 1
                ? new DatabentoHardAttemptResult(false, true, "Connect", new IOException("Unavailable"), [])
                : new DatabentoHardAttemptResult(true, true, "LocalQualification", null,
                    [Worker(generation)]));
        }, TimeProvider.System, NoDelay);

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(2, calls);
        Assert.Equal(DatabentoHardRecoveryOutcome.DatabentoHealthy, result.Outcome);
        Assert.Equal(generation, result.DatasetGenerations["GLBX.MDP3"]);
    }

    [Fact]
    public async Task First_attempt_success_does_not_start_another_attempt()
    {
        var calls = 0;
        var generation = Guid.NewGuid();
        var engine = new DatabentoHardRecoveryEngine((_, _) =>
        {
            calls++;
            return Task.FromResult(new DatabentoHardAttemptResult(true, true,
                "LocalQualification", null, [Worker(generation)]));
        }, TimeProvider.System, NoDelay);

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.DatabentoHealthy, result.Outcome);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, calls);
        Assert.Single(result.AttemptEvidence);
    }

    [Fact]
    public async Task Third_attempt_success_preserves_prior_failures_and_stops_at_three()
    {
        var calls = new List<int>();
        var generation = Guid.NewGuid();
        var engine = new DatabentoHardRecoveryEngine((number, _) =>
        {
            calls.Add(number);
            return Task.FromResult(number == 3
                ? new DatabentoHardAttemptResult(true, true, "LocalQualification", null,
                    [Worker(generation)])
                : new DatabentoHardAttemptResult(false, true, "Connect",
                    new IOException($"Attempt {number} failed"), []));
        }, TimeProvider.System, NoDelay);

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.DatabentoHealthy, result.Outcome);
        Assert.Equal(3, result.Attempts);
        Assert.Equal([1, 2, 3], calls);
        Assert.Equal(3, result.AttemptEvidence.Count);
        Assert.Equal("Connect", result.AttemptEvidence[0].Stage);
        Assert.Equal("Connect", result.AttemptEvidence[1].Stage);
    }

    [Fact]
    public async Task Noncooperative_attempt_exceeding_episode_deadline_is_terminal_without_retry()
    {
        var calls = 0;
        var never = new TaskCompletionSource<DatabentoHardAttemptResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new DatabentoHardRecoveryEngine((_, _) =>
        {
            calls++;
            return never.Task;
        }, TimeProvider.System, new DatabentoHardRecoveryPolicy
        {
            OverallTimeout = TimeSpan.FromMilliseconds(100),
            AttemptTwoDelay = TimeSpan.Zero,
            AttemptThreeDelay = TimeSpan.Zero
        });

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.Unrecoverable, result.Outcome);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, calls);
        Assert.Equal("AttemptBoundary", result.FailedStage);
        never.SetResult(new DatabentoHardAttemptResult(true, true,
            "LocalQualification", null, [Worker(Guid.NewGuid())]));
    }

    [Fact]
    public async Task Attempt_evidence_keeps_primary_and_cleanup_failures_separate()
    {
        var worker = Worker(Guid.NewGuid());
        var engine = new DatabentoHardRecoveryEngine((_, _) => Task.FromResult(
            new DatabentoHardAttemptResult(false, false, "Connect",
                new IOException("Primary connection failure"), [worker])
            {
                CleanupFailure = new TimeoutException("Cleanup timed out")
            }), TimeProvider.System, NoDelay);

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.Unrecoverable, result.Outcome);
        var evidence = Assert.Single(result.AttemptEvidence);
        Assert.NotEqual(Guid.Empty, evidence.AttemptId);
        Assert.NotEqual(default, evidence.StartedUtc);
        Assert.True(evidence.Elapsed >= TimeSpan.Zero);
        Assert.Equal("IOException", evidence.FailureType);
        Assert.Equal("Primary connection failure", evidence.Detail);
        Assert.Equal("TimeoutException", evidence.CleanupFailureType);
        Assert.Equal("Cleanup timed out", evidence.CleanupDetail);
        Assert.Equal(worker.GenerationId, evidence.DatasetGenerations[worker.Dataset]);
        Assert.Contains(worker.ProcessId, evidence.WorkerProcessIds);
    }

    static DatabentoHardRecoveryRequest Request() =>
        new(Guid.NewGuid(), new DateOnly(2026, 9, 30), Guid.NewGuid(), "Test", "Worker failure");

    static DatasetWorkerProcessSnapshot Worker(Guid generation) => new()
    {
        Dataset = "GLBX.MDP3", WorkerInstanceId = Guid.NewGuid(), GenerationId = generation,
        ProcessId = 123, StartedOnUtc = DateTime.UtcNow, Running = true, Healthy = true,
        GracefulStopSucceeded = false, ForcedTermination = false
    };
}
