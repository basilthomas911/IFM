using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Legacy;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Legacy;
/// <summary>Compatibility adapter for callers of the former state-owned execution API; decisions stay with concrete command handlers.</summary>
public static class DatabaseBackupStateExecutionExtensions
{

    extension(DatabaseBackupReplayState state)
    {
        public DatabaseRecoveryOperationId Execute(IDatabaseBackupCommand command)
        {
            var transition = command switch
            {
                RequestDatabaseBackupCommand c => Require(c.Compute(state.BackupDecision, out var change), change),
                CancelDatabaseBackupCommand c => Require(c.Compute(state.BackupDecision, out var change), change),
                RequestDatabaseRestoreCommand c => Require(c.Compute(state.RecoveryDecision, out var change), change),
                ApproveDatabaseRestoreCommand c => Require(c.Compute(state.RecoveryDecision, out var change), change),
                CancelDatabaseRestoreCommand c => Require(c.Compute(state.RecoveryDecision, out var change), change),
                ApproveDatabaseCutoverCommand c => Require(c.Compute(state.RecoveryDecision, out var change), change),
                RequestDatabaseRestoreDrillCommand c => Require(c.Compute(state.RecoveryDecision, out var change), change),
                UpdateDatabaseBackupPolicyCommand c => Require(c.Compute(state.PolicyDecision, out var change), change),
                PlaceBackupLegalHoldCommand c => Require(c.Compute(state.BackupSetDecision, out var change), change),
                ReleaseBackupLegalHoldCommand c => Require(c.Compute(state.BackupSetDecision, out var change), change),
                RequestBackupRetentionEvaluationCommand c => Require(c.Compute(state.RetentionDecision, out var change), change),
                ExecuteBackupRetentionPlanCommand c => Require(c.Compute(state.RetentionDecision, out var change), change),
                _ => throw new InvalidOperationException($"Unsupported DatabaseBackup command '{command.GetType().Name}'.")
            };
            if (!state.Update(DatabaseBackupEventFactory.Create(command, transition), command)) throw new InvalidOperationException("DatabaseBackup.STATE.APPLY_FAILED");
            return command.EntityId;
        }

        public DatabaseRecoveryOperationId Execute(DatabaseBackupInternalCommand command)
        {
            var transition = command is ReconcileDatabaseBackupServiceStateCommand reconcile
                ? DatabaseServiceReconciliationComputation.Compute(reconcile, state.ReconciliationDecision)
                : DatabaseOperationReceiptComputation.Compute(command, state.Operation, state.OperationDecision.ProcessedServiceEvents);
            if (!state.Update(DatabaseBackupEventFactory.Create(command, transition), command)) throw new InvalidOperationException("DatabaseBackup.STATE.APPLY_FAILED");
            return state.Operation.OperationId;
        }
    }
    static DatabaseBackupTransition Require(bool accepted, DatabaseBackupTransition change) => accepted ? change : throw new InvalidOperationException(change.RejectionReason);
}
