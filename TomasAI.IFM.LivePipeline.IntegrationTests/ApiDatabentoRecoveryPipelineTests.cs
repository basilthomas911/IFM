using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using Xunit;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class ApiDatabentoRecoveryPipelineTests
{
    [Fact]
    public async Task Infrastructure_failure_is_terminal_and_never_admits_downstream()
    {
        var hardCalls = 0;
        var softCalls = 0;
        var supervisorCalls = 0;
        var admissions = 0;
        var pipeline = Pipeline(
            hard: (request, _) => { hardCalls++; return Task.FromResult(Healthy(request)); },
            infrastructure: _ => { softCalls++; return Task.FromResult(new DatabentoSoftGateResult(false, 3, [])); },
            supervisor: (_, _) => { supervisorCalls++; return ValueTask.FromResult(HealthySupervisor()); },
            admit: (_, _) => { admissions++; return Task.CompletedTask; });
        var request = Request();

        var first = await pipeline.HardResetRecoveryAsync(request);
        var retry = await pipeline.HardResetRecoveryAsync(request);

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, first!.Outcome);
        Assert.Same(first, retry);
        Assert.Equal(1, hardCalls);
        Assert.Equal(1, softCalls);
        Assert.Equal(0, supervisorCalls);
        Assert.Equal(0, admissions);
    }

    [Fact]
    public async Task Failed_recovery_rejects_later_requests_even_when_detector_generation_changes()
    {
        var hardCalls = 0;
        var pipeline = Pipeline(
            hard: (request, _) => { hardCalls++; return Task.FromResult(Healthy(request)); },
            infrastructure: _ => Task.FromResult(new DatabentoSoftGateResult(false, 1, [])));
        var firstRequest = Request();

        var first = await pipeline.HardResetRecoveryAsync(firstRequest);
        var retry = await pipeline.HardResetRecoveryAsync(firstRequest with
        {
            CorrelationId = Guid.NewGuid(), ExpectedGenerationId = Guid.NewGuid()
        });

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, first!.Outcome);
        Assert.Same(first, retry);
        Assert.Equal(1, hardCalls);
    }

    [Fact]
    public async Task Later_reset_after_full_health_runs_a_fresh_hard_attempt()
    {
        var hardCalls = 0;
        var pipeline = Pipeline(hard: (request, _) =>
        {
            hardCalls++;
            return Task.FromResult(Healthy(request));
        });

        var first = await pipeline.HardResetRecoveryAsync(Request());
        var later = await pipeline.HardResetRecoveryAsync(Request());

        Assert.Equal(DatabentoRecoveryRequestOutcome.FullyHealthy, first!.Outcome);
        Assert.Equal(DatabentoRecoveryRequestOutcome.FullyHealthy, later!.Outcome);
        Assert.Equal(2, hardCalls);
    }

    [Fact]
    public async Task Value_date_change_does_not_reopen_terminal_recovery()
    {
        var hardCalls = 0;
        var pipeline = Pipeline(
            hard: (request, _) =>
            {
                hardCalls++;
                return Task.FromResult(Healthy(request));
            },
            infrastructure: _ => Task.FromResult(new DatabentoSoftGateResult(false, 1, [])));
        var firstRequest = Request();

        var first = await pipeline.HardResetRecoveryAsync(firstRequest);
        var rollover = await pipeline.HardResetRecoveryAsync(firstRequest with
        {
            CorrelationId = Guid.NewGuid(), ValueDate = firstRequest.ValueDate.AddDays(1)
        });

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, first!.Outcome);
        Assert.Same(first, rollover);
        Assert.Equal(1, hardCalls);
    }

    [Fact]
    public async Task Full_success_requires_supervisor_proof_and_admission()
    {
        var sequence = new List<string>();
        var pipeline = Pipeline(
            hard: (request, _) => { sequence.Add("hard"); return Task.FromResult(Healthy(request)); },
            infrastructure: _ =>
            {
                sequence.Add("infrastructure");
                return Task.FromResult(new DatabentoSoftGateResult(true, 1, []));
            },
            supervisor: (_, _) =>
            {
                sequence.Add("supervisor");
                return ValueTask.FromResult(HealthySupervisor());
            },
            prepare: (_, _) => { sequence.Add("hold"); return Task.CompletedTask; },
            proof: (_, _, _, _) => { sequence.Add("proof"); return Task.FromResult(true); },
            admit: (_, _) => { sequence.Add("admit"); return Task.CompletedTask; });

        var result = await pipeline.HardResetRecoveryAsync(Request());

        Assert.Equal(DatabentoRecoveryRequestOutcome.FullyHealthy, result!.Outcome);
        Assert.Equal(["hard", "hold", "infrastructure", "supervisor", "proof", "admit"], sequence);
    }

    [Fact]
    public async Task Failed_generation_proof_never_opens_admission()
    {
        var admissions = 0;
        var pipeline = Pipeline(
            hard: (request, _) => Task.FromResult(Healthy(request)),
            infrastructure: _ => Task.FromResult(new DatabentoSoftGateResult(true, 1, [])),
            supervisor: (_, _) => ValueTask.FromResult(HealthySupervisor()),
            proof: (_, _, _, _) => Task.FromResult(false),
            admit: (_, _) => { admissions++; return Task.CompletedTask; });

        var result = await pipeline.HardResetRecoveryAsync(Request());

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, result!.Outcome);
        Assert.Equal(0, admissions);
    }

    [Fact]
    public async Task Concurrent_detectors_are_ignored_while_one_recovery_runs()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hardCalls = 0;
        var pipeline = Pipeline(hard: async (request, _) =>
        {
            Interlocked.Increment(ref hardCalls);
            started.TrySetResult();
            await release.Task;
            return Healthy(request);
        });
        var firstRequest = Request();

        var first = pipeline.HardResetRecoveryAsync(firstRequest);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var ignored = pipeline.HardResetRecoveryAsync(Request());
        release.TrySetResult();
        var results = await Task.WhenAll(first, ignored);

        Assert.Equal(DatabentoRecoveryRequestOutcome.AlreadyInProgress, results[1].Outcome);
        Assert.Equal(1, hardCalls);
        Assert.Equal(DatabentoRecoveryRequestOutcome.FullyHealthy, results[0].Outcome);
    }

    [Fact]
    public async Task Supervisor_failure_keeps_candidate_fenced_and_skips_downstream_proof()
    {
        var proofCalls = 0;
        var admissions = 0;
        var pipeline = Pipeline(
            supervisor: (_, _) => ValueTask.FromResult(new SupervisorRecoveryResult(
                Guid.NewGuid(), false, 0, 1, 0, 0, [], "Unhealthy")),
            proof: (_, _, _, _) => { proofCalls++; return Task.FromResult(true); },
            admit: (_, _) => { admissions++; return Task.CompletedTask; });

        var result = await pipeline.HardResetRecoveryAsync(Request());

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, result!.Outcome);
        Assert.Equal(0, proofCalls);
        Assert.Equal(0, admissions);
    }

    [Fact]
    public async Task Terminal_hard_failure_requests_shutdown_once_and_rejects_later_episodes()
    {
        var hardCalls = 0;
        var fatalCalls = 0;
        var pipeline = Pipeline(
            hard: (request, _) =>
            {
                hardCalls++;
                return Task.FromResult(new DatabentoHardRecoveryResult(request.CorrelationId,
                    Guid.Empty, 3, DatabentoHardRecoveryOutcome.Unrecoverable,
                    "AttemptExhaustion", "Three failed attempts"));
            },
            fatal: (_, _) => { fatalCalls++; return Task.CompletedTask; });

        var first = await pipeline.HardResetRecoveryAsync(Request());
        var later = await pipeline.HardResetRecoveryAsync(Request());

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, first!.Outcome);
        Assert.Same(first, later);
        Assert.Equal(1, hardCalls);
        Assert.Equal(1, fatalCalls);
    }

    [Fact]
    public async Task Hard_boundary_exception_still_requests_fatal_shutdown_once()
    {
        var fatalCalls = 0;
        var pipeline = Pipeline(
            hard: (_, _) => throw new IOException("Injected hard boundary failure"),
            fatal: (_, result) =>
            {
                Assert.Equal(nameof(IApiDatabentoRecoveryActions.QualifyDatabentoAsync), result.FailedStage);
                fatalCalls++;
                return Task.CompletedTask;
            });

        var first = await pipeline.HardResetRecoveryAsync(Request());
        var later = await pipeline.HardResetRecoveryAsync(Request());

        Assert.Equal(DatabentoRecoveryRequestOutcome.Unrecoverable, first!.Outcome);
        Assert.Same(first, later);
        Assert.Equal(1, fatalCalls);
    }

    static ApiDatabentoRecoveryPipeline Pipeline(
        Func<DatabentoHardRecoveryRequest, CancellationToken, Task<DatabentoHardRecoveryResult>>? hard = null,
        Func<CancellationToken, Task<DatabentoSoftGateResult>>? infrastructure = null,
        Func<TimeSpan, CancellationToken, ValueTask<SupervisorRecoveryResult>>? supervisor = null,
        Func<DatabentoHardRecoveryRequest, DatabentoHardRecoveryResult, TimeSpan,
            CancellationToken, Task<bool>>? proof = null,
        Func<DatabentoHardRecoveryResult, CancellationToken, Task>? admit = null,
        Func<DatabentoHardRecoveryRequest, DatabentoHardRecoveryResult, Task>? fatal = null,
        Func<DatabentoHardRecoveryResult, CancellationToken, Task>? prepare = null)
    {
        var actions = Substitute.For<IApiDatabentoRecoveryActions>();
        actions.CaptureRecoveryInputsAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        actions.FenceFailedGenerationAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        actions.AbandonPreviousCandidateAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        actions.StopDatabentoWorkersAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        actions.StartDatabentoWorkersAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        actions.QualifyDatabentoAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var context = call.Arg<ApiDatabentoRecoveryContext>();
                context.HardResult = hard is null ? Healthy(context.Request)
                    : await hard(context.Request, call.Arg<CancellationToken>());
                if (context.HardResult.Outcome != DatabentoHardRecoveryOutcome.DatabentoHealthy)
                    throw new InvalidOperationException(context.HardResult.Detail);
            });
        actions.PrepareCandidateAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(call => prepare is null ? Task.CompletedTask
                : prepare(call.Arg<ApiDatabentoRecoveryContext>().HardResult!, call.Arg<CancellationToken>()));
        actions.QualifyInfrastructureAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                if (infrastructure is not null && !(await infrastructure(call.Arg<CancellationToken>())).Qualified)
                    throw new InvalidOperationException("Infrastructure qualification failed.");
            });
        actions.ReconcileActorsAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                if (supervisor is not null && !(await supervisor(TimeSpan.FromSeconds(30), call.Arg<CancellationToken>())).Qualified)
                    throw new InvalidOperationException("Supervisor qualification failed.");
            });
        actions.StartPublisherAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        actions.ProveDownstreamWritesAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var context = call.Arg<ApiDatabentoRecoveryContext>();
                if (proof is not null && !await proof(context.Request, context.HardResult!,
                    TimeSpan.FromSeconds(30), call.Arg<CancellationToken>()))
                    throw new InvalidOperationException("Downstream proof failed.");
            });
        actions.AdmitGenerationAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(call => admit is null ? Task.CompletedTask
                : admit(call.Arg<ApiDatabentoRecoveryContext>().HardResult!, call.Arg<CancellationToken>()));
        actions.NotifySystemConsoleAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<DatabentoHardRecoveryResult>())
            .Returns(Task.CompletedTask);
        actions.ShutdownApiAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<DatabentoHardRecoveryResult>())
            .Returns(call => fatal is null ? Task.CompletedTask
                : fatal(call.Arg<ApiDatabentoRecoveryContext>().Request, call.Arg<DatabentoHardRecoveryResult>()));
        return new(actions, CancellationToken.None, new ApiDatabentoRecoveryPipelinePolicy(),
            NullLogger<ApiDatabentoRecoveryPipeline>.Instance);
    }

    static DatabentoHardRecoveryRequest Request() => new(Guid.NewGuid(),
        new DateOnly(2026, 9, 30), Guid.NewGuid(), "Test", "Worker failed");

    static DatabentoHardRecoveryResult Healthy(DatabentoHardRecoveryRequest request) =>
        new(request.CorrelationId, Guid.NewGuid(), 1,
            DatabentoHardRecoveryOutcome.DatabentoHealthy, string.Empty, "Locally qualified");

    static SupervisorRecoveryResult HealthySupervisor() =>
        new(Guid.NewGuid(), true, 1, 0, 0, 0, [], "Healthy");
}
