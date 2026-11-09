using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
/// <summary>Owns the Service operation and only its concern-specific facts.</summary>
public sealed class DatabaseBackupServiceCommandState : DatabaseOperationCommandState<DatabaseBackupServiceCommandState>
{
    public DatabaseBackupServiceState Service { get; private set; } = new();

    protected override DatabaseOperationStateSnapshottedEvent CaptureConcern(DatabaseOperationStateSnapshottedEvent snapshot)
        => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Copy(snapshot, snapshot with { Service = Service });

    protected override bool HasConcernSnapshot(DatabaseOperationStateSnapshottedEvent snapshot) => snapshot.Service is not null;

    protected override bool RestoreConcern(DatabaseOperationStateSnapshottedEvent snapshot)
    {
        if (snapshot.Service is not { } value)
            return false;
        Service = value;
        return true;
    }

    protected override void ApplyConcern(DatabaseBackupEventContract source) { Service = Service.Apply(source); }
}
