using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationRules;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationChanges;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
/// <summary>Repairs only validated missing cutovers and terminal retention display facts from the independent service journal.</summary>
internal static class DatabaseServiceReconciliationComputation
{
    internal static DatabaseBackupTransition Compute(ReconcileDatabaseBackupServiceStateCommand command, DatabaseServiceReconciliationDecisionState state)
    {
        if (!DatabaseOperationReceiptComputation.TryBegin(command, state.Operation, state.ProcessedServiceEvents, out var backupChange)) return new([]);
        if (command is ReconcileDatabaseBackupServiceStateCommand && command.Source.OperationKind == DatabaseRecoveryOperationKind.Cutover && command.Source.Phase == DatabaseRecoveryPhase.CuttingOver && command.CutoverState == DatabaseCutoverState.InProgress && command.VerificationLevel == DatabaseVerificationLevel.ApplicationValidation && command.ValidationRevision > 0 && command.RestorePointId is not null && command.FreshTarget is not null && command.Source.ProducingHostId is not null && command.Source.SourceRevisionOrSequence > 0)
        {
            // The independent validated recovery journal survives replacement of PostgreSQL.
            // Seed only a missing cutover; never overwrite an existing operation definition.
            if (!state.Operation.Exists)
                return new([backupChange]);
            if (state.Operation.Kind == DatabaseRecoveryOperationKind.Cutover && state.Operation.Phase == DatabaseRecoveryPhase.CuttingOver && state.Operation.OperationId == command.EntityId && state.Operation.Source == command.Source.Source && state.Operation.ProtectionSetId == command.Source.ProtectionSetId && state.Operation.HostId == command.Source.ProducingHostId && state.Operation.LastServiceSequence == command.Source.SourceRevisionOrSequence && state.Operation.ValidationRevision == command.ValidationRevision && state.Restore.RestorePointId == command.RestorePointId && state.Restore.FreshTarget == command.FreshTarget)
                return new([]);
        }

        // Recover only terminal retention display state; no execution request is emitted.
        if (command is ReconcileDatabaseBackupServiceStateCommand && command.Source.Source == BackupSource.AwsCloud && command.Source.OperationKind == DatabaseRecoveryOperationKind.Retention && command.Source.ProducingHostId is not null && command.Source.SourceRevisionOrSequence > 0 && command.RetentionPlanId is not null && command.RetentionPlanRevision > 0 && ((command.Source.Phase == DatabaseRecoveryPhase.Completed && command.Outcome == DatabaseRecoveryOutcome.Succeeded) || (command.Source.Phase == DatabaseRecoveryPhase.Failed && command.Outcome == DatabaseRecoveryOutcome.Failed)))
        {
            if (!state.Operation.Exists)
                return new([backupChange]);
            // An explicit repair may republish the same terminal observation to its read model.
            // Immutable identity, terminal outcome and independent service sequence stay unchanged.
            if (state.Operation.Kind == DatabaseRecoveryOperationKind.Retention && state.Operation.IsTerminal && state.Operation.OperationId == command.EntityId && state.Operation.Source == command.Source.Source && state.Operation.ProtectionSetId == command.Source.ProtectionSetId && state.Operation.PolicyRevision == command.Source.PolicyRevision && state.Operation.HostId == command.Source.ProducingHostId && state.Operation.LastServiceSequence == command.Source.SourceRevisionOrSequence && state.Operation.Phase == command.Source.Phase && state.Operation.Outcome == command.Outcome && state.Retention.PlanId == command.RetentionPlanId && state.Retention.PlanRevision == command.RetentionPlanRevision)
                return new([backupChange]);
        }

        return DatabaseOperationReceiptComputation.Accept(command, state.Operation, backupChange);
    }
}
