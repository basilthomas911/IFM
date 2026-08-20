using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerRuntimeService(
    ISchedulerFactory schedulerFactory,
    QuartzScheduleReconciler reconciler,
    SchedulerBootstrapState bootstrap,
    SchedulerHealthState health,
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

        await _scheduler.Standby(CancellationToken.None);
        await _scheduler.Shutdown(waitForJobsToComplete: false, CancellationToken.None);
    }
}
