using Npgsql;
using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerOperationalMonitor(
    SchedulerHostOptions options,
    NpgsqlDataSource dataSource,
    ISchedulerFactory schedulerFactory,
    QuartzScheduleReconciler reconciler,
    SchedulerBootstrapState bootstrap,
    SchedulerHealthState health,
    ILogger<SchedulerOperationalMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bootstrap.Succeeded)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.HealthProbeIntervalSeconds), stoppingToken);
                var scheduler = await schedulerFactory.GetScheduler(stoppingToken);
                await ProbeDatabaseAsync(stoppingToken);
                var freeBytes = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(options.TaskRunRoot))!).AvailableFreeSpace;
                if (freeBytes < options.MinimumFreeDiskBytes)
                {
                    await scheduler.Standby(stoppingToken);
                    health.Set(
                        SchedulerServiceState.Degraded,
                        databaseAvailable: true,
                        quartzAvailable: true,
                        schedulingStarted: false,
                        $"Scheduling paused: task-run volume has only {freeBytes} free bytes.");
                    continue;
                }

                if (health.Current.State is SchedulerServiceState.Unhealthy or SchedulerServiceState.Degraded)
                {
                    await reconciler.ReconcileAsync(scheduler, stoppingToken);
                    await scheduler.Start(stoppingToken);
                    health.Set(SchedulerServiceState.Ready, true, true, true, "Scheduler Host recovered and is ready.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                try
                {
                    var scheduler = await schedulerFactory.GetScheduler(CancellationToken.None);
                    await scheduler.Standby(CancellationToken.None);
                }
                catch (Exception standbyException)
                {
                    logger.LogError(standbyException, "Quartz could not enter standby after an operational health failure.");
                }

                health.Set(
                    SchedulerServiceState.Unhealthy,
                    databaseAvailable: false,
                    quartzAvailable: health.Current.QuartzAvailable,
                    schedulingStarted: false,
                    $"Operational health probe failed: {exception.Message}");
                logger.LogError(exception, "Scheduler operational health probe failed; new scheduling is paused.");
            }
        }
    }

    private async Task ProbeDatabaseAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
