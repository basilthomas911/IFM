using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;

public sealed record DatabaseBackupSetState
{
    public DatabaseBackupSetId? BackupSetId { get; init; }
    public int CheckpointCount { get; init; }
    public bool Complete { get; init; }
    public long Revision { get; init; }
    /// <summary>Applies this concern's accepted persisted facts without changing another concern.</summary>
    internal DatabaseBackupSetState Apply(DatabaseBackupEventContract e)
    {
        return e is DatabaseBackupSetCheckpointRecordedEvent or DatabaseBackupSetCompletedEvent
            ? this with { BackupSetId = e.Source.BackupSetId, CheckpointCount = CheckpointCount + 1,
                Complete = e is DatabaseBackupSetCompletedEvent, Revision = Revision + 1 } : this;
    }
}
