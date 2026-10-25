using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseBackupSetReadModel
{
    [Key(0)] public DatabaseBackupSetId BackupSetId { get; init; }
    [Key(1)] public BackupSource Source { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    [Key(2)] public DatabaseRecoveryOperationId[] OperationIds { get; init; } = [];
    [Key(3)] public int RequiredOperationCount { get; init; }
    [Key(4)] public int CompletedOperationCount { get; init; }
    [Key(5)] public bool Complete { get; init; }
    [Key(6)] public long Revision { get; init; }
}
