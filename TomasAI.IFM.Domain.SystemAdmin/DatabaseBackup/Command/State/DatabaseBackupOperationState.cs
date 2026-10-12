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
