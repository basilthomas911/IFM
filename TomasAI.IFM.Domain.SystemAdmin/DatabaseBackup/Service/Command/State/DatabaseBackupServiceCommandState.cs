using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
/// <summary>Owns the Service operation and only its concern-specific facts.</summary>
public sealed class DatabaseBackupServiceCommandState : DatabaseOperationCommandState<DatabaseBackupServiceCommandState>
{
    public DatabaseBackupServiceState Service { get; private set; } = new();

    protected override void ApplyConcern(DatabaseBackupEventContract source) { Service = Service.Apply(source); }
}
