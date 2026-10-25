namespace TomasAI.IFM.Domain.Supervisor.Shared.Enums;

/// <summary>Describes the dedicated polling service lifecycle.</summary>
public enum SupervisorPollingServiceState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}
