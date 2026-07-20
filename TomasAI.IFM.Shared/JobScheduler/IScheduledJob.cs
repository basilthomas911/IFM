namespace TomasAI.IFM.Shared.JobScheduler;

public interface IScheduledJob
{
    int JobId { get; }
    string JobName { get; }
    JobScheduleType JobSchedule { get; }
    DateTime JobScheduleDate { get; }
    double JobScheduleInterval { get; }
    string TaskName { get; }
    bool TaskEnabled { get; }
    IScheduledJobDaysOfWeek JobScheduleDaysOfWeek { get; set; }
    Action<DateTime, IScheduledJob> JobTask { get; set; }
    DateTime CreatedOn { get; }
    string CreatedBy { get; }
    DateTime UpdatedOn { get; }
    string UpdatedBy { get; }
}
