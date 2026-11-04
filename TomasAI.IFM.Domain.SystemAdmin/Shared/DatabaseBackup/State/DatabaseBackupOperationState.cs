using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;

[MessagePackObject]
public sealed record DatabaseBackupOperationState
{
    [Key(0)]
    public DatabaseRecoveryOperationId OperationId { get; init; }
    [Key(1)]
    public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(2)]
    public DatabaseProtectionSetId ProtectionSetId { get; init; }
    [Key(3)]
    public BackupSource Source { get; init; }
    [Key(4)]
    public DatabaseRecoveryOperationKind Kind { get; init; }
    [Key(5)]
    public DatabaseRecoveryPhase Phase { get; init; }
    [Key(6)]
    public DatabaseRecoveryOutcome Outcome { get; init; }
    [Key(7)]
    public long PolicyRevision { get; init; }
    [Key(8)]
    public long Revision { get; init; }
    [Key(9)]
    public int ProgressPercent { get; init; }
    [Key(10)]
    public DatabaseBackupHostId? HostId { get; init; }
    [Key(11)]
    public long LastServiceSequence { get; init; }
    [Key(12)]
    public long ValidationRevision { get; init; }
    [Key(13)]
    public DatabaseCutoverState CutoverState { get; init; }
    [Key(14)]
    public DatabaseBackupLineage? BackupLineage { get; init; }
    [IgnoreMember]
    public bool Exists => OperationId.Value != Guid.Empty;
    [IgnoreMember]
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
