using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerRuntimeService(
    ISchedulerFactory schedulerFactory,
    QuartzScheduleReconciler reconciler,
    SchedulerBootstrapState bootstrap,
    SchedulerHealthState health,
    SchedulerHostOptions options,
    ActiveRunRegistry activeRuns,
    SchedulerOwnershipLease ownership,
    ILogger<SchedulerRuntimeService> logger) : IHostedService
{
    private IScheduler? _scheduler;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!bootstrap.Succeeded)
        {
            logger.LogWarning("Quartz startup skipped because scheduler bootstrap did not succeed.");
            return;
        }

        try
        {
            _scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            await _scheduler.Standby(cancellationToken);
            await reconciler.ReconcileAsync(_scheduler, cancellationToken);
            await ownership.EnsureOwnedAsync(cancellationToken);
            if (options.ActorManaged)
            {
                health.Set(SchedulerServiceState.Starting, true, true, false, "Quartz is in standby pending actor reservation recovery and installation reconciliation.");
                return;
            }
            await _scheduler.Start(cancellationToken);
            health.Set(
                SchedulerServiceState.Ready,
                databaseAvailable: true,
                quartzAvailable: true,
                schedulingStarted: true,
                "Scheduler Host is ready.");
        }
        catch (Exception exception)
        {
            health.Set(
                SchedulerServiceState.Unhealthy,
                databaseAvailable: true,
                quartzAvailable: false,
                schedulingStarted: false,
                $"Quartz startup failed: {exception.Message}");
            logger.LogError(exception, "Quartz startup failed; scheduling remains stopped.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        health.Set(
            SchedulerServiceState.Stopping,
            databaseAvailable: bootstrap.Succeeded,
            quartzAvailable: _scheduler is not null,
            schedulingStarted: false,
            "Scheduler Host is stopping.");
        if (_scheduler is null)
        {
            return;
        }

        using var standbyDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.ProcessTerminationTimeoutSeconds));
        try { await _scheduler.Standby(standbyDeadline.Token).WaitAsync(standbyDeadline.Token); }
        catch (OperationCanceledException) { logger.LogError("Quartz standby timed out during shutdown."); }
        activeRuns.CancelAll();
        var shutdown = _scheduler.Shutdown(waitForJobsToComplete: true, CancellationToken.None);
        if (await Task.WhenAny(shutdown, Task.Delay(options.ShutdownTimeout, CancellationToken.None)) != shutdown)
        {
            logger.LogError("Quartz jobs did not complete within the configured {Timeout}; shutdown continues after Job Object cancellation.", options.ShutdownTimeout);
            using var shutdownDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(options.ProcessTerminationTimeoutSeconds));
            await _scheduler.Shutdown(waitForJobsToComplete: false, shutdownDeadline.Token).WaitAsync(shutdownDeadline.Token);
        }
        else await shutdown;
    }
}
