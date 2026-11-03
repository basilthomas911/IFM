using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.State;
/// <summary>Owns the Backup operation and only its concern-specific facts.</summary>
public sealed class DatabaseBackupCommandState : DatabaseOperationCommandState<DatabaseBackupCommandState>
{

    internal DatabaseBackupDecisionState BackupDecision => new(Operation);

    internal TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseServiceReconciliationDecisionState ReconciliationDecision => new(Operation, new(), new(), OperationDecision.ProcessedServiceEvents);
    protected override void ApplyConcern(DatabaseBackupEventContract source) {  }
}
