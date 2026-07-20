namespace TomasAI.IFM.Shared.JobScheduler;

public interface IJobScheduler
{
    IScheduledJob[] ScheduledJobs { get; }

    Task LoadAsync();
    Task StartAsync(DateOnly valueDate);
    Task StopAsync();
    Task RefreshAsync(DateOnly valueDate);
}
