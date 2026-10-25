namespace TomasAI.IFM.Domain.Supervisor.Shared.Enums;

/// <summary>Describes Supervisor authority availability independently of managed actor health.</summary>
public enum SupervisorAuthorityState
{
    Available,
    AvailableWithDegradedCapability,
    HostRecoveryRequired
}
