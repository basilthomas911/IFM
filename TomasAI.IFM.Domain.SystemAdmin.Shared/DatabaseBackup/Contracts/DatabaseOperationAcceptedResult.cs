using MessagePack;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

[MessagePackObject]
public sealed record DatabaseOperationAcceptedResult
{
    [Key(0)] public DatabaseRecoveryOperationId OperationId { get; init; }
    [Key(1)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(2)] public BackupSource Source { get; init; }
    [Key(3)] public long PolicyRevision { get; init; }
    [Key(4)] public DatabaseRecoveryPhase InitialPhase { get; init; }
}
