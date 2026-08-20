using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerDashboardQueryService(
    SchedulerHealthState health,
    SchedulerBootstrapState bootstrap,
    SchedulerStore store,
    ILogger<SchedulerDashboardQueryService> logger)
{
    public async Task<SchedulerDashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        if (!bootstrap.Succeeded)
        {
            return new SchedulerDashboardDto(
                health.Current,
                [],
                [],
                [],
                DateTimeOffset.UtcNow);
        }

        try
        {
            var catalog = await store.GetTaskCatalogAsync(cancellationToken);
            var schedules = await store.GetSchedulesAsync(cancellationToken);
            var runs = await store.GetRecentRunsAsync(cancellationToken);
            return new SchedulerDashboardDto(health.Current, catalog, schedules, runs, DateTimeOffset.UtcNow);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduler dashboard query failed.");
            health.Set(
                SchedulerServiceState.Unhealthy,
                databaseAvailable: false,
                quartzAvailable: health.Current.QuartzAvailable,
                schedulingStarted: false,
                $"Dashboard database query failed: {exception.Message}");
            return new SchedulerDashboardDto(health.Current, [], [], [], DateTimeOffset.UtcNow);
        }
    }
}
