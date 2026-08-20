using Quartz;
using Quartz.Impl.Matchers;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class QuartzScheduleReconciler(SchedulerStore store)
{
    public async Task ReconcileAsync(IScheduler scheduler, CancellationToken cancellationToken)
    {
        var schedules = await store.GetSchedulesAsync(cancellationToken);
        var activeJobNames = schedules
            .Where(value => value.Enabled)
            .Select(value => value.ScheduleDefinitionId.ToString("N"))
            .ToHashSet(StringComparer.Ordinal);
        var existingJobs = await scheduler.GetJobKeys(
            GroupMatcher<JobKey>.GroupEquals("ifm-schedules"),
            cancellationToken);
        foreach (var staleJob in existingJobs.Where(job => !activeJobNames.Contains(job.Name)))
        {
            await scheduler.DeleteJob(staleJob, cancellationToken);
        }

        foreach (var schedule in schedules.Where(value => value.Enabled))
        {
            var jobKey = new JobKey(schedule.ScheduleDefinitionId.ToString("N"), "ifm-schedules");
            var triggerKey = new TriggerKey(schedule.ScheduleDefinitionId.ToString("N"), "ifm-schedules");
            var job = JobBuilder.Create<ExternalProcessJob>()
                .WithIdentity(jobKey)
                .UsingJobData(ScheduledTaskExecutionService.TaskKeyData, schedule.TaskKey)
                .UsingJobData(ScheduledTaskExecutionService.ScheduleDefinitionIdData, schedule.ScheduleDefinitionId.ToString("D"))
                .StoreDurably()
                .Build();
            var trigger = BuildTrigger(schedule, jobKey, triggerKey);

            if (await scheduler.CheckExists(jobKey, cancellationToken))
            {
                await scheduler.AddJob(job, true, true, cancellationToken);
                if (await scheduler.CheckExists(triggerKey, cancellationToken))
                {
                    await scheduler.RescheduleJob(triggerKey, trigger, cancellationToken);
                }
                else
                {
                    await scheduler.ScheduleJob(trigger, cancellationToken);
                }
            }
            else
            {
                await scheduler.ScheduleJob(job, trigger, cancellationToken);
            }

            await store.UpdateScheduleFireTimesAsync(
                schedule.ScheduleDefinitionId,
                trigger.GetPreviousFireTimeUtc(),
                trigger.GetNextFireTimeUtc(),
                cancellationToken);
        }
    }

    private static ITrigger BuildTrigger(ScheduleSummaryDto schedule, JobKey jobKey, TriggerKey triggerKey)
    {
        var builder = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey);
        if (schedule.Kind == ScheduleKind.Cron)
        {
            var cron = CronScheduleBuilder.CronSchedule(schedule.ScheduleExpression)
                .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId));
            cron = schedule.MisfirePolicy == SchedulerMisfirePolicy.FireOnceNow
                ? cron.WithMisfireHandlingInstructionFireAndProceed()
                : cron.WithMisfireHandlingInstructionDoNothing();
            return builder.WithSchedule(cron).Build();
        }

        var simple = schedule.Kind switch
        {
            ScheduleKind.OneTime => SimpleScheduleBuilder.Create(),
            ScheduleKind.SimpleInterval => SimpleScheduleBuilder.Create()
                .WithInterval(TimeSpan.FromSeconds(int.Parse(schedule.ScheduleExpression)))
                .RepeatForever(),
            _ => throw new InvalidOperationException($"Unsupported schedule kind '{schedule.Kind}'.")
        };
        simple = schedule.MisfirePolicy == SchedulerMisfirePolicy.FireOnceNow
            ? simple.WithMisfireHandlingInstructionNowWithExistingCount()
            : simple.WithMisfireHandlingInstructionNextWithRemainingCount();
        builder = builder.WithSchedule(simple);
        return schedule.Kind == ScheduleKind.OneTime
            ? builder.StartAt(DateTimeOffset.Parse(schedule.ScheduleExpression)).Build()
            : builder.StartNow().Build();
    }
}
