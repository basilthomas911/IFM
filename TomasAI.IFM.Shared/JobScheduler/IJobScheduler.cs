namespace TomasAI.IFM.Shared.JobScheduler;

public interface IJobScheduler
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    IScheduledJob[] ScheduledJobs { get; }

    Task LoadAsync();
    Task StartAsync(DateOnly valueDate);
    Task StopAsync();
    Task RefreshAsync(DateOnly valueDate);
}
