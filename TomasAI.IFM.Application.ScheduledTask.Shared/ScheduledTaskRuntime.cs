using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Application.ScheduledTask.Shared;

public enum ScheduledTaskExitCode
{
    Succeeded = 0,
    Failed = 1,
    Cancelled = 2,
    InvalidConfiguration = 3
}

public sealed class ScheduledTaskOutcome
{
    private int _exitCode = (int)ScheduledTaskExitCode.Failed;

    public int ExitCode => Volatile.Read(ref _exitCode);

    public void Succeeded() => Interlocked.Exchange(ref _exitCode, (int)ScheduledTaskExitCode.Succeeded);

    public void Failed() => Interlocked.Exchange(ref _exitCode, (int)ScheduledTaskExitCode.Failed);

    public void Cancelled() => Interlocked.Exchange(ref _exitCode, (int)ScheduledTaskExitCode.Cancelled);
}

public abstract class OneShotScheduledTaskWorker(
    IHostApplicationLifetime lifetime,
    ScheduledTaskOutcome outcome,
    ILogger logger) : BackgroundService
{
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ExecuteTaskAsync(stoppingToken).ConfigureAwait(false);
            outcome.Succeeded();
            logger.LogInformation("Scheduled task completed successfully.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            outcome.Cancelled();
            logger.LogWarning("Scheduled task was cancelled.");
        }
        catch (Exception exception)
        {
            outcome.Failed();
            logger.LogError(exception, "Scheduled task failed.");
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    protected abstract Task ExecuteTaskAsync(CancellationToken cancellationToken);
}

public sealed class ScheduledTaskControlService(
    IHostApplicationLifetime lifetime,
    ILogger<ScheduledTaskControlService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeName = Environment.GetEnvironmentVariable("IFM_TASK_CONTROL_PIPE");
        if (string.IsNullOrWhiteSpace(pipeName))
        {
            return;
        }

        try
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync(stoppingToken).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 1024, leaveOpen: true);
            var command = await reader.ReadLineAsync(stoppingToken).ConfigureAwait(false);
            if (string.Equals(command, "Cancel", StringComparison.Ordinal))
            {
                logger.LogWarning("Cancellation received from Scheduler Host control pipe.");
                lifetime.StopApplication();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal completion closes the one-shot control endpoint.
        }
    }
}

public static class ScheduledTaskRuntimeExtensions
{
    public static IServiceCollection AddScheduledTaskRuntime(this IServiceCollection services)
    {
        services.AddSingleton<ScheduledTaskOutcome>();
        services.AddHostedService<ScheduledTaskControlService>();
        return services;
    }
}
