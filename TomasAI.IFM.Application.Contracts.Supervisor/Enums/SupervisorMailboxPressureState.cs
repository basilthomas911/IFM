namespace TomasAI.IFM.Domain.Supervisor.Shared.Enums;

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
