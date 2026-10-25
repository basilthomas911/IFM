namespace TomasAI.IFM.Domain.Supervisor.Shared.Enums;

/// <summary>Lists the only named lifecycle mutations accepted by the Supervisor.</summary>
public enum SupervisorActorOperationKind
{
    Pause,
    Drain,
    Resume,
    Stop,
    Restart,
    Quarantine,
    Retire,
    Recycle,
    AcknowledgeIncident
}
