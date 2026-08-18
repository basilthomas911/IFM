using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public sealed record DatabaseBackupOperationState
{
    public DatabaseRecoveryOperationId OperationId { get; init; }
    public DatabaseBackupSetId? BackupSetId { get; init; }
    public DatabaseProtectionSetId ProtectionSetId { get; init; }
    public BackupSource Source { get; init; }
    public DatabaseRecoveryOperationKind Kind { get; init; }
    public DatabaseRecoveryPhase Phase { get; init; }
    public DatabaseRecoveryOutcome Outcome { get; init; }
    public long PolicyRevision { get; init; }
    public long Revision { get; init; }
    public int ProgressPercent { get; init; }
    public DatabaseBackupHostId? HostId { get; init; }
    public long LastServiceSequence { get; init; }
    public long ValidationRevision { get; init; }
    public DatabaseCutoverState CutoverState { get; init; }
    public DatabaseBackupLineage? BackupLineage { get; init; }
    public bool Exists => OperationId.Value != Guid.Empty;
    public bool IsTerminal => Phase is DatabaseRecoveryPhase.Completed or DatabaseRecoveryPhase.Failed
        or DatabaseRecoveryPhase.Cancelled or DatabaseRecoveryPhase.Rejected;
}

public sealed record DatabaseRestoreOperationState
{
    public DatabaseRestorePointId? RestorePointId { get; init; }
    public DatabaseRestoreClass RestoreClass { get; init; }
    public string FreshTargetProfile { get; init; } = string.Empty;
    public bool Approved { get; init; }
    public DatabaseCutoverState CutoverState { get; init; }
}

public sealed record DatabaseBackupSetState
{
    public DatabaseBackupSetId? BackupSetId { get; init; }
    public int CheckpointCount { get; init; }
    public bool Complete { get; init; }
    public long Revision { get; init; }
}

public sealed record DatabaseBackupPolicyState
{
    public DatabaseBackupPolicyId? PolicyId { get; init; }
    public DatabaseBackupPolicyDefinition? Definition { get; init; }
    public long Revision { get; init; }
    public bool Enforced { get; init; }
}

public sealed record DatabaseBackupServiceState
{
    public BackupSource Source { get; init; }
    public DatabaseBackupHostId? HostId { get; init; }
    public DatabaseServiceCapabilityState CapabilityState { get; init; }
    public long LastServiceSequence { get; init; }
    public bool Reconciled { get; init; }
}

public sealed record DatabaseRetentionState
{
    public DatabaseRetentionPlanId? PlanId { get; init; }
    public long PlanRevision { get; init; }
    public DatabaseRecoveryOutcome Outcome { get; init; }
    public long Revision { get; init; }
}
