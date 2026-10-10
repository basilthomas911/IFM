using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Contracts;
using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.HardRecovery;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using Xunit;
using NSubstitute;
using Microsoft.Extensions.Logging.Abstractions;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class ApiDatabentoRecoveryPipelineTests
{
    [Fact]
    public async Task Failed_recovery_rejects_later_requests_even_when_detector_generation_changes()
    {
        var hardCalls = 0;
        var pipeline = Pipeline(
            hard: (request, _) => { hardCalls++; return Task.FromResult(Healthy(request)); },
            admit: (_, _) => throw new IOException("Admission failed"));
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
            admit: (_, _) => throw new IOException("Admission failed"));
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
    public async Task Feed_success_requires_only_local_readiness_and_exact_admission()
    {
        var sequence = new List<string>();
        var pipeline = Pipeline(
            hard: (request, _) => { sequence.Add("local readiness"); return Task.FromResult(Healthy(request)); },
            admit: (_, _) => { sequence.Add("admit"); return Task.CompletedTask; });
        var result = await pipeline.HardResetRecoveryAsync(Request());
        Assert.Equal(DatabentoRecoveryRequestOutcome.FullyHealthy, result.Outcome);
        Assert.Equal(["local readiness", "admit"], sequence);
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
        Assert.Equal(3, hardCalls);
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
        Func<DatabentoHardRecoveryResult, CancellationToken, Task>? admit = null,
        Func<DatabentoHardRecoveryRequest, DatabentoHardRecoveryResult, Task>? fatal = null)
    {
        var actions = Substitute.For<IApiDatabentoRecoveryActions>();
        actions.CaptureRecoveryInputsAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        actions.FenceFailedGenerationAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
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
        actions.StartPublisherAsync(Arg.Any<ApiDatabentoRecoveryContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
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
