using Quartz;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerRunRequestDispatcher(
    SchedulerStore store,
    ISchedulerFactory schedulerFactory,
    SchedulerBootstrapState bootstrap,
    ILogger<SchedulerRunRequestDispatcher> logger) : BackgroundService
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
                var pending = await store.GetPendingRunRequestsAsync(stoppingToken);
                foreach (var item in pending)
                {
                    await DispatchAsync(item, stoppingToken);
                    await store.MarkOutboxPublishedAsync(item.OutboxId, stoppingToken);
                }

                await Task.Delay(pending.Count == 0 ? TimeSpan.FromSeconds(1) : TimeSpan.FromMilliseconds(50), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Run-request outbox dispatch failed; the durable request will be retried.");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task DispatchAsync(OutboxRunRequest item, CancellationToken cancellationToken)
    {
        var request = item.Request;
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        var jobKey = new JobKey(request.RunId.ToString("N"), "ifm-run-requests");
        if (await scheduler.CheckExists(jobKey, cancellationToken))
        {
            return;
        }

        var jobBuilder = JobBuilder.Create<ExternalProcessJob>()
            .WithIdentity(jobKey)
            .UsingJobData(ScheduledTaskExecutionService.TaskKeyData, request.TaskKey)
            .UsingJobData(ScheduledTaskExecutionService.ScheduleDefinitionIdData, request.ScheduleDefinitionId?.ToString("D") ?? string.Empty)
            .UsingJobData(ScheduledTaskExecutionService.RunIdData, request.RunId.ToString("D"))
            .UsingJobData(ScheduledTaskExecutionService.OccurrenceIdData, request.OccurrenceId.ToString("D"))
            .UsingJobData(ScheduledTaskExecutionService.AttemptIdData, request.AttemptId.ToString("D"))
            .UsingJobData(ScheduledTaskExecutionService.OriginData, request.Origin.ToString());
        if (request.MaximumRuntimeSeconds is not null)
        {
            jobBuilder.UsingJobData(ScheduledTaskExecutionService.MaximumRuntimeSecondsData, request.MaximumRuntimeSeconds.Value.ToString());
        }
        var job = jobBuilder.Build();
        var trigger = TriggerBuilder.Create()
            .WithIdentity(request.RunId.ToString("N"), "ifm-run-requests")
            .StartNow()
            .Build();
        await scheduler.ScheduleJob(job, trigger, cancellationToken);
    }
}
