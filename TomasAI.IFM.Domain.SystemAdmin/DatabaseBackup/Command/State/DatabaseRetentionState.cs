using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public sealed record DatabaseRetentionState
{
    public DatabaseRetentionPlanId? PlanId { get; init; }
    public long PlanRevision { get; init; }
    public DatabaseRecoveryOutcome Outcome { get; init; }
    public long Revision { get; init; }
}
