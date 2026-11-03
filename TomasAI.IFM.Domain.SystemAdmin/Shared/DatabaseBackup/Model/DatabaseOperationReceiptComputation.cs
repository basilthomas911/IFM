using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationRules;
using static TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model.DatabaseOperationChanges;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
/// <summary>Validates correlated service observations and the explicitly supported independent-journal repairs.</summary>
internal static class DatabaseOperationReceiptComputation
{
    internal static DatabaseBackupTransition Compute(DatabaseBackupInternalCommand command, DatabaseBackupOperationState operation,
        IReadOnlyDictionary<Guid, string> processedServiceEvents)
    {
        return TryBegin(command, operation, processedServiceEvents, out var change)
            ? Accept(command, operation, change) : new([]);
    }

    internal static bool TryBegin(DatabaseBackupInternalCommand command, DatabaseBackupOperationState Operation,
        IReadOnlyDictionary<Guid, string> processedServiceEvents, out DatabaseBackupChange backupChange)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureExpectedRevision(Operation, command.ExpectedStateRevision);
        backupChange = Translate(command);
        var fingerprint = Fingerprint(backupChange);
        if (processedServiceEvents.TryGetValue(command.Source.SourceEventId, out var existing))
        {
            if (!StringComparer.Ordinal.Equals(existing, fingerprint))
                throw new InvalidOperationException("A service event ID was replayed with conflicting content.");
            return false;
        }

        return true;
    }

    internal static DatabaseBackupTransition Accept(DatabaseBackupInternalCommand command, DatabaseBackupOperationState operation, DatabaseBackupChange change)
    {
        EnsureServiceIdentityAndSequence(operation, command.Source);
        EnsureLegalTransition(operation, change.DatabaseSource.Phase, change.Outcome);
        return new([change]);
    }

    static DatabaseBackupChange Translate(DatabaseBackupInternalCommand command) => command switch
    {
        RecordDatabaseOperationAdmissionCommand => CreateChange<DatabaseOperationAdmissionRecordedEvent>(command, command.Source.Phase),
        RecordDatabaseOperationStartedCommand => CreateChange<DatabaseOperationStartedEvent>(command, DatabaseRecoveryPhase.Started),
        RecordDatabaseOperationProgressCommand => CreateChange<DatabaseOperationProgressRecordedEvent>(command, command.Source.Phase),
        RecordDatabaseBackupBoundaryCommand => CreateChange<DatabaseBackupBoundaryRecordedEvent>(command, command.Source.Phase),
        RecordDatabaseArtifactReplicaCommand => CreateChange<DatabaseArtifactReplicaRecordedEvent>(command, command.Source.Phase),
        RecordDatabaseOperationVerificationCommand => command.Source.OperationKind is DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill ? CreateChange<DatabaseRestoreValidationRecordedEvent>(command, DatabaseRecoveryPhase.Validating) : CreateChange<DatabaseOperationVerificationRecordedEvent>(command, DatabaseRecoveryPhase.Verifying),
        RecordDatabaseOperationErrorCommand => CreateChange<DatabaseOperationErrorRecordedEvent>(command, command.Source.Phase),
        RecordDatabaseRestoreReadyForCutoverCommand => CreateChange<DatabaseRestoreReadyForCutoverRecordedEvent>(command, DatabaseRecoveryPhase.ReadyForCutover),
        CompleteDatabaseOperationCommand => CreateChange<DatabaseOperationCompletedEvent>(command, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded),
        FailDatabaseOperationCommand => CreateChange<DatabaseOperationFailedEvent>(command, DatabaseRecoveryPhase.Failed, DatabaseRecoveryOutcome.Failed),
        RecordDatabaseOperationCancelledCommand => CreateChange<DatabaseOperationCancelledEvent>(command, DatabaseRecoveryPhase.Cancelled, DatabaseRecoveryOutcome.Cancelled),
        RecordDatabaseBackupPolicyStatusCommand => CreateChange<DatabaseBackupPolicyEnforcedEvent>(command, command.Source.Phase),
        RecordDatabaseRetentionResultCommand => CreateChange<DatabaseRetentionResultRecordedDomainEvent>(command, command.Source.Phase, command.Outcome),
        ReconcileDatabaseBackupServiceStateCommand when command.Source.OperationKind == DatabaseRecoveryOperationKind.Retention => CreateChange<DatabaseRetentionResultRecordedDomainEvent>(command, command.Source.Phase),
        ReconcileDatabaseBackupServiceStateCommand => CreateChange<DatabaseBackupServiceReconciledEvent>(command, command.Source.Phase),
        RecordDatabaseBackupServiceCapabilityCommand => CreateChange<DatabaseBackupServiceCapabilityRecordedEvent>(command, command.Source.Phase),
        RecordDatabaseRecoveryRunStatisticsCommand => CreateChange<DatabaseRecoveryStatisticsRecordedEvent>(command, command.Source.Phase),
        _ => throw new InvalidOperationException($"Unsupported internal DatabaseBackup command '{command.GetType().Name}'.")};

}
