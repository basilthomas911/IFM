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

/// <summary>Describes Supervisor authority availability independently of managed actor health.</summary>
public enum SupervisorAuthorityState
{
    Available,
    AvailableWithDegradedCapability,
    HostRecoveryRequired
}

/// <summary>Describes evaluated actor health.</summary>
public enum SupervisorActorHealth
{
    Healthy,
    Degraded,
    Critical,
    Unknown
}

/// <summary>Describes the baseline pressure and recovery phase of one entity mailbox.</summary>
public enum SupervisorMailboxPressureState
{
    Normal,
    Elevated,
    Critical,
    AtLimit,
    Degraded,
    Restarting,
    Recovering
}

/// <summary>Describes the completeness of a collected metrics snapshot.</summary>
public enum SupervisorSnapshotQuality
{
    Complete,
    Partial,
    Minimal
}

/// <summary>Describes the dedicated polling service lifecycle.</summary>
public enum SupervisorPollingServiceState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Faulted
}

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
