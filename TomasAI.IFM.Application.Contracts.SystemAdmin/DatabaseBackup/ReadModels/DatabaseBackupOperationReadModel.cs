using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseBackupOperationReadModel
{
    [Key(0)] public DatabaseRecoveryOperationId OperationId { get; init; }
    [Key(1)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(2)] public DatabaseProtectionSetId ProtectionSetId { get; init; }
    [Key(3)] public BackupSource Source { get; init; }
    [Key(4)] public DatabaseRecoveryOperationKind Kind { get; init; }
    [Key(5)] public DatabaseRecoveryPhase Phase { get; init; }
    [Key(6)] public DatabaseRecoveryOutcome Outcome { get; init; }
    [Key(7)] public int ProgressPercent { get; init; }
    [Key(8)] public long StateRevision { get; init; }
    [Key(9)] public DateTimeOffset CreatedUtc { get; init; }
    [Key(10)] public DateTimeOffset? CompletedUtc { get; init; }
    [Key(11)] public string SafeDiagnosticReference { get; init; } = string.Empty;
    [Key(12)] public DatabaseBackupLineage? BackupLineage { get; init; }
    /// <summary>Gets the engine recorded by native run statistics.</summary>
    [Key(13)] public DatabaseEngine Engine { get; init; }
}
