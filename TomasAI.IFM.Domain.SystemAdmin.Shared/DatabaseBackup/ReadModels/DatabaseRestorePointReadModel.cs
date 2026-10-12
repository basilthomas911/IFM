using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseRestorePointReadModel
{
    [Key(0)] public DatabaseRestorePointId RestorePointId { get; init; }
    [Key(1)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(2)] public DatabaseProtectionSetId ProtectionSetId { get; init; }
    [Key(3)] public BackupSource Source { get; init; }
    [Key(4)] public DateTimeOffset RecoveryPointUtc { get; init; }
    [Key(5)] public DatabaseVerificationLevel VerificationLevel { get; init; }
    [Key(6)] public DateTimeOffset? VerifiedUtc { get; init; }
    [Key(7)] public DateTimeOffset? RestoreTestedUtc { get; init; }
    [Key(8)] public bool Eligible { get; init; }
    [Key(9)] public bool LegalHold { get; init; }
    [Key(10)] public long ManifestRevision { get; init; }
    [Key(11)] public DatabaseBackupLineage? BackupLineage { get; init; }
}
