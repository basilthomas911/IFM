namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerRetentionService(SchedulerHostOptions options, SchedulerStore store)
{
    public async Task<int> RunAsync(string actor, string reason, CancellationToken cancellationToken)
    {
        var candidates = await store.GetRetentionCandidatesAsync(cancellationToken);
        var removed = 0;
        foreach (var candidate in candidates)
        {
            DeleteRetainedFile(candidate.StdoutPath);
            DeleteRetainedFile(candidate.StderrPath);
            await store.MarkOutputDeletedAsync(candidate.RunId, actor, reason, cancellationToken);
            removed++;
        }

        return removed;
    }

    private void DeleteRetainedFile(string path)
    {
        var root = Path.GetFullPath(options.TaskRunRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchedulerValidationException("Retention candidate escapes the configured task-run root.");
        }

        if (File.Exists(target))
        {
            File.Delete(target);
        }
    }
}

public sealed class SchedulerRetentionHostedService(
    SchedulerRetentionService retention,
    SchedulerBootstrapState bootstrap,
    ILogger<SchedulerRetentionHostedService> logger) : BackgroundService
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
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                var removed = await retention.RunAsync("scheduler-host", "Scheduled retention cleanup", stoppingToken);
                logger.LogInformation("Scheduler retention removed output for {Count} run(s).", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled output retention failed.");
            }
        }
    }
}
