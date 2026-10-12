namespace TomasAI.IFM.Domain.Supervisor.Shared.Enums;

/// <summary>Describes the terminal outcome of a Supervisor operation.</summary>
public enum SupervisorOperationOutcome
{
    Succeeded,
    Rejected,
    Cancelled,
    TimedOut,
    Failed,
    PartiallyCompleted,
    EmergencyFallback
}
