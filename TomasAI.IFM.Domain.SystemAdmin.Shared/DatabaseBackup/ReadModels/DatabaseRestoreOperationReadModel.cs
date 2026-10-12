using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseRestoreOperationReadModel
{
    [Key(0)] public DatabaseBackupOperationReadModel Operation { get; init; } = new();
    [Key(1)] public DatabaseRestorePointId RestorePointId { get; init; }
    [Key(2)] public DatabaseRestoreClass RestoreClass { get; init; }
    [Key(3)] public string FreshTargetProfile { get; init; } = string.Empty;
    [Key(4)] public long ValidationRevision { get; init; }
    [Key(5)] public DatabaseCutoverState CutoverState { get; init; }
}
