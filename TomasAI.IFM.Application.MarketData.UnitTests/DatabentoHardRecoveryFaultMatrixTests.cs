using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatabentoHardRecoveryFaultMatrixTests
{
    static readonly DatabentoHardRecoveryPolicy NoDelay = new()
    {
        AttemptTwoDelay = TimeSpan.Zero,
        AttemptThreeDelay = TimeSpan.Zero
    };

    [Theory]
    [InlineData("FrozenManifest")]
    [InlineData("PublisherIsolation")]
    [InlineData("ContainOldWorkers")]
    [InlineData("LaunchCandidate")]
    [InlineData("Connect")]
    [InlineData("Subscribe")]
    [InlineData("LocalQualification")]
    public async Task Safe_stage_failure_retries_with_a_fresh_attempt(string stage)
    {
        var calls = new List<int>();
        var generation = Guid.NewGuid();
        var engine = new DatabentoHardRecoveryEngine((number, _) =>
        {
            calls.Add(number);
            return Task.FromResult(number == 1
                ? new DatabentoHardAttemptResult(false, true, stage,
                    new IOException("Injected stage fault"), [])
                : new DatabentoHardAttemptResult(true, true, "LocalQualification", null,
                    [Worker(generation)]));
        }, TimeProvider.System, NoDelay);

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.DatabentoHealthy, result.Outcome);
        Assert.Equal([1, 2], calls);
        Assert.Equal(2, result.Attempts);
        Assert.Equal(stage, result.AttemptEvidence[0].Stage);
        Assert.Equal(generation, result.DatasetGenerations["GLBX.MDP3"]);
    }

    [Theory]
    [InlineData("PublisherIsolation")]
    [InlineData("ContainOldWorkers")]
    [InlineData("CandidateAdmission")]
    public async Task Unsafe_isolation_failure_never_starts_another_attempt(string stage)
    {
        var calls = 0;
        var engine = new DatabentoHardRecoveryEngine((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new DatabentoHardAttemptResult(false, false, stage,
                new TimeoutException("Isolation unknown"), []));
        }, TimeProvider.System, NoDelay);

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.Unrecoverable, result.Outcome);
        Assert.Equal(stage, result.FailedStage);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Three_safe_stage_failures_never_start_a_fourth_attempt()
    {
        var calls = new List<int>();
        var engine = new DatabentoHardRecoveryEngine((number, _) =>
        {
            calls.Add(number);
            return Task.FromResult(new DatabentoHardAttemptResult(false, true,
                "Connect", new IOException("Still unavailable"), []));
        }, TimeProvider.System, NoDelay);

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);

        Assert.Equal(DatabentoHardRecoveryOutcome.Unrecoverable, result.Outcome);
        Assert.Equal("AttemptExhaustion", result.FailedStage);
        Assert.Equal([1, 2, 3], calls);
        Assert.Equal(3, result.AttemptEvidence.Count);
    }

    [Fact]
    public async Task Late_success_after_deadline_cannot_change_terminal_result()
    {
        var late = new TaskCompletionSource<DatabentoHardAttemptResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var engine = new DatabentoHardRecoveryEngine((_, _) =>
        {
            Interlocked.Increment(ref calls);
            return late.Task;
        }, TimeProvider.System, new DatabentoHardRecoveryPolicy
        {
            OverallTimeout = TimeSpan.FromMilliseconds(100),
            AttemptTwoDelay = TimeSpan.Zero,
            AttemptThreeDelay = TimeSpan.Zero
        });

        var result = await engine.ExecuteAsync(Request(), CancellationToken.None);
        late.SetResult(new DatabentoHardAttemptResult(true, true,
            "LocalQualification", null, [Worker(Guid.NewGuid())]));

        Assert.Equal(DatabentoHardRecoveryOutcome.Unrecoverable, result.Outcome);
        Assert.Equal("AttemptBoundary", result.FailedStage);
        Assert.Equal(1, calls);
        Assert.Empty(result.DatasetGenerations);
    }

    [Fact]
    public void Closing_generation_rejects_stale_publications()
    {
        var admissions = new DatasetWorkerAdmissionRegistry();
        var old = Admission(Guid.NewGuid());
        var current = Admission(Guid.NewGuid());
        admissions.Admit(old);
        Assert.True(admissions.TryAccept(old, 1, out var oldCancellation));
        admissions.Close(old.Dataset, old.GenerationId);
        admissions.Admit(current);
        Assert.True(oldCancellation.IsCancellationRequested);
        Assert.False(admissions.TryAccept(old, 2));
        Assert.True(admissions.TryAccept(current, 1));
        Assert.Equal(1, admissions.RejectedPublications);
    }

    static DatabentoHardRecoveryRequest Request() =>
        new(Guid.NewGuid(), new DateOnly(2026, 9, 30), Guid.NewGuid(), "FaultMatrix", "Synthetic");

    static DatasetWorkerAdmission Admission(Guid generation) =>
        new("GLBX.MDP3", new DateOnly(2026, 9, 30), Guid.NewGuid(), generation, 1);

    static DatasetWorkerProcessSnapshot Worker(Guid generation) => new()
    {
        Dataset = "GLBX.MDP3", WorkerInstanceId = Guid.NewGuid(), GenerationId = generation,
        ProcessId = 123, StartedOnUtc = DateTime.UtcNow, Running = true, Healthy = true,
        GracefulStopSucceeded = false, ForcedTermination = false
    };
}
