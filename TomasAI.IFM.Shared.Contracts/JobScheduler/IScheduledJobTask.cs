namespace TomasAI.IFM.Shared.JobScheduler;

public interface IScheduledJobTask
{
    string TaskName { get; }
    Action<DateTime, IScheduledJob> JobTask { get; }
}
