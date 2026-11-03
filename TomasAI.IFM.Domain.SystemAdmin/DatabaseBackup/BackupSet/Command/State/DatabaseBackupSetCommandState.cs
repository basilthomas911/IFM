using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;
/// <summary>Owns the BackupSet operation and only its concern-specific facts.</summary>
public sealed class DatabaseBackupSetCommandState : DatabaseOperationCommandState<DatabaseBackupSetCommandState>
{
    public DatabaseBackupSetState BackupSet { get; private set; } = new();
    internal DatabaseBackupSetDecisionState BackupSetDecision => new(Operation, BackupSet);

    protected override void ApplyConcern(DatabaseBackupEventContract source) { BackupSet = BackupSet.Apply(source); }
}
