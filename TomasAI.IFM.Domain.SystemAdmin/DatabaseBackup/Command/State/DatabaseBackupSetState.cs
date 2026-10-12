using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public sealed record DatabaseBackupSetState
{
    public DatabaseBackupSetId? BackupSetId { get; init; }
    public int CheckpointCount { get; init; }
    public bool Complete { get; init; }
    public long Revision { get; init; }
}
