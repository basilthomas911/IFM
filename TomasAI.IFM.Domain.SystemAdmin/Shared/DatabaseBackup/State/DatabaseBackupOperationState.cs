using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;

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
    /// <summary>Applies this concern's accepted persisted facts without changing another concern.</summary>
    internal DatabaseBackupOperationState Apply(DatabaseBackupEventContract e)
    {
        var source = e.Source;
        var nextRevision = this.Revision + 1;
        var phase = e switch
        {
            DatabaseOperationCompletedEvent => DatabaseRecoveryPhase.Completed,
            DatabaseOperationFailedEvent => DatabaseRecoveryPhase.Failed,
            DatabaseOperationCancelledEvent => DatabaseRecoveryPhase.Cancelled,
            _ => source.Phase
        };
        var outcome = e switch
        {
            DatabaseOperationCompletedEvent => DatabaseRecoveryOutcome.Succeeded,
            DatabaseOperationFailedEvent => DatabaseRecoveryOutcome.Failed,
            DatabaseOperationCancelledEvent => DatabaseRecoveryOutcome.Cancelled,
            _ => e.Outcome
        };
        return this with
        {
            OperationId = source.OperationId,
            BackupSetId = source.BackupSetId,
            ProtectionSetId = source.ProtectionSetId,
            Source = source.Source,
            Kind = source.OperationKind,
            Phase = phase,
            Outcome = outcome,
            PolicyRevision = source.PolicyRevision,
            Revision = nextRevision,
            ProgressPercent = Math.Max(this.ProgressPercent, e.ProgressPercent),
            HostId = source.ProducingHostId ?? this.HostId,
            LastServiceSequence = source.ProducingHostId is null ? this.LastServiceSequence : source.SourceRevisionOrSequence,
            ValidationRevision = e.ValidationRevision == 0 ? this.ValidationRevision : e.ValidationRevision,
            CutoverState = e.CutoverState == DatabaseCutoverState.None ? this.CutoverState : e.CutoverState,
            BackupLineage = e.BackupLineage ?? this.BackupLineage
        };
    }
}
