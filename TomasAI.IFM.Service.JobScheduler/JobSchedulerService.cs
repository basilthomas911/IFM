using Chroniton.Jobs;
using Chroniton.Schedules;
using TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.JobScheduler;

namespace TomasAI.IFM.Service.JobScheduler;

/// <summary>
/// create job scheduler service
/// </summary>
/// <param name="scheduledJobResolver"></param>
/// <param name="referenceDb"></param>
public class JobSchedulerService(IScheduledJobTaskResolver scheduledJobTaskResolver, IReferenceDbContext referenceDb) : IJobScheduler
{
    readonly IReferenceDbContext _referenceDb = IsArgumentNull.Set(referenceDb);
    readonly IScheduledJobTaskResolver _scheduledJobTaskResolver = IsArgumentNull.Set( scheduledJobTaskResolver);
    Dictionary<string, IScheduledJob>? _scheduledJobs;

    public IScheduledJob[] ScheduledJobs => _scheduledJobs is not null ? [.. _scheduledJobs.Values] : [];

    /// <summary>
    /// load all scheduled jobs
    /// </summary>
    public async Task LoadAsync()
    {
        _scheduledJobs = new Dictionary<string, IScheduledJob>();
        var scheduledJobTasks = _scheduledJobTaskResolver.Resolve();
        foreach (var scheduledJob in await _referenceDb.DbReader.GetScheduledJobsAsync())
        {
            var scheduledJobTask = scheduledJobTasks.Where(e => e.TaskName == scheduledJob.TaskName).SingleOrDefault();
            if (scheduledJobTask != null)
            {
                //scheduledJob. = scheduledJobTask.JobTask;
                //_scheduledJobs.Add(scheduledJob.JobName, scheduledJob);
            }
        }
    }

    /// <summary>
    /// start all scheduled jobs
    /// </summary>
    /// <returns></returns>
    public async Task StartAsync(DateOnly valueDate)
    {
        var singularity = Chroniton.Singularity.Instance;
        foreach (var scheduledJob in _scheduledJobs!.Values)
        {
            var job = new SimpleJob(jobTime => scheduledJob.JobTask(jobTime, scheduledJob));
            var schedule = new SimpleSchedule(() => new DateTime(valueDate.Year, valueDate.Month, valueDate.Day, scheduledJob.JobScheduleDate.Hour, scheduledJob.JobScheduleDate.Minute, scheduledJob.JobScheduleDate.Second));
            singularity.ScheduleJob(schedule, job, true);
        }
        singularity.Start();
        await Task.CompletedTask;
    }

    /// <summary>
    /// stop all scheduled jobs
    /// </summary>
    public async Task StopAsync()
    {
        Chroniton.Singularity.Instance.Stop();
        await Task.CompletedTask;
    }

    /// <summary>
    /// reload all scheduled jobs
    /// </summary>
    /// <param name="valueDate"></param>
    public async Task RefreshAsync(DateOnly valueDate)
    {
        await StopAsync();
        await LoadAsync();
        await StartAsync(valueDate);
    }
}
