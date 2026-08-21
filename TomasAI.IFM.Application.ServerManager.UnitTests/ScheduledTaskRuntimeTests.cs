using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.ScheduledTask.Shared;

namespace TomasAI.IFM.Application.ServerManager.UnitTests;

public sealed class ScheduledTaskRuntimeTests
{
    [Fact]
    public async Task One_shot_success_returns_zero()
    {
        using var host = CreateHost<SuccessfulWorker>();
        var outcome = host.Services.GetRequiredService<ScheduledTaskOutcome>();

        await host.RunAsync();

        outcome.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task One_shot_failure_is_visible_as_nonzero_exit_code()
    {
        using var host = CreateHost<FailingWorker>();
        var outcome = host.Services.GetRequiredService<ScheduledTaskOutcome>();

        await host.RunAsync();

        outcome.ExitCode.Should().Be(1);
    }

    private static IHost CreateHost<TWorker>() where TWorker : class, IHostedService
        => Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddScheduledTaskRuntime();
                services.AddHostedService<TWorker>();
            })
            .Build();

    private sealed class SuccessfulWorker(
        IHostApplicationLifetime lifetime,
        ScheduledTaskOutcome outcome,
        ILogger<SuccessfulWorker> logger)
        : OneShotScheduledTaskWorker(lifetime, outcome, logger)
    {
        protected override Task ExecuteTaskAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FailingWorker(
        IHostApplicationLifetime lifetime,
        ScheduledTaskOutcome outcome,
        ILogger<FailingWorker> logger)
        : OneShotScheduledTaskWorker(lifetime, outcome, logger)
    {
        protected override Task ExecuteTaskAsync(CancellationToken cancellationToken)
            => throw new InvalidOperationException("expected failure");
    }
}
