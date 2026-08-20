using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class SchedulerHealthState
{
    private readonly object _sync = new();
    private SchedulerHealthDto _current = new(
        SchedulerServiceState.Starting,
        typeof(SchedulerHealthState).Assembly.GetName().Version?.ToString() ?? "0.0.0",
        DatabaseAvailable: false,
        QuartzAvailable: false,
        SchedulingStarted: false,
        "Scheduler Host is starting.",
        DateTimeOffset.UtcNow);

    public SchedulerHealthDto Current
    {
        get
        {
            lock (_sync)
            {
                return _current;
            }
        }
    }

    public void Set(
        SchedulerServiceState state,
        bool databaseAvailable,
        bool quartzAvailable,
        bool schedulingStarted,
        string message)
    {
        lock (_sync)
        {
            _current = _current with
            {
                State = state,
                DatabaseAvailable = databaseAvailable,
                QuartzAvailable = quartzAvailable,
                SchedulingStarted = schedulingStarted,
                Message = message,
                ObservedAtUtc = DateTimeOffset.UtcNow
            };
        }
    }
}

public sealed class SchedulerBootstrapState
{
    public bool Succeeded { get; set; }

    public string? Failure { get; set; }
}
