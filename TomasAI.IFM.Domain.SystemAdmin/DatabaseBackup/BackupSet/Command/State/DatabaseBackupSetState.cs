using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;

[MessagePackObject]
public sealed record DatabaseBackupSetState
{
    [Key(0)]
    public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(1)]
    public int CheckpointCount { get; init; }
    [Key(2)]
    public bool Complete { get; init; }
    [Key(3)]
    public long Revision { get; init; }
    /// <summary>Applies this concern's accepted persisted facts without changing another concern.</summary>
    internal DatabaseBackupSetState Apply(DatabaseBackupEventContract e)
    {
        return e is DatabaseBackupSetCheckpointRecordedEvent or DatabaseBackupSetCompletedEvent
            ? this with { BackupSetId = e.Source.BackupSetId, CheckpointCount = CheckpointCount + 1,
                Complete = e is DatabaseBackupSetCompletedEvent, Revision = Revision + 1 } : this;
    }
}
