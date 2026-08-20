using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerBootstrapService(
    SchedulerHostOptions options,
    SchedulerDatabaseMigrator migrator,
    TaskCatalogProvider catalog,
    SchedulerStore store,
    SchedulerBootstrapState bootstrap,
    SchedulerHealthState health,
    ILogger<SchedulerBootstrapService> logger) : IHostedService
{
    private FileStream? _instanceLock;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        health.Set(
            SchedulerServiceState.Starting,
            databaseAvailable: false,
            quartzAvailable: false,
            schedulingStarted: false,
            "Applying scheduler database migrations.");
        try
        {
            Directory.CreateDirectory(options.TaskRunRoot);
            _instanceLock = new FileStream(
                Path.Combine(options.TaskRunRoot, ".scheduler-host.lock"),
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            await migrator.MigrateAsync(cancellationToken);
            await catalog.SynchronizeSnapshotAsync(cancellationToken);
            var abandoned = await store.RecoverIncompleteRunsAsync(cancellationToken);
            bootstrap.Succeeded = true;
            health.Set(
                SchedulerServiceState.Starting,
                databaseAvailable: true,
                quartzAvailable: false,
                schedulingStarted: false,
                abandoned == 0
                    ? "Database ready; starting Quartz."
                    : $"Database ready; recovered {abandoned} incomplete run(s) as Abandoned.");
        }
        catch (Exception exception)
        {
            bootstrap.Failure = exception.Message;
            health.Set(
                SchedulerServiceState.Unhealthy,
                databaseAvailable: false,
                quartzAvailable: false,
                schedulingStarted: false,
                $"Scheduler bootstrap failed: {exception.Message}");
            logger.LogError(exception, "Scheduler bootstrap failed; Quartz will remain stopped.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _instanceLock?.Dispose();
        _instanceLock = null;
        return Task.CompletedTask;
    }
}
