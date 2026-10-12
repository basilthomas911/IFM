using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;

public static class DatabaseBackupEventTranslator
{
    public static DatabaseBackupInternalCommand Translate(DatabaseBackupServiceEventContract serviceEvent)
    {
        ArgumentNullException.ThrowIfNull(serviceEvent);
        serviceEvent.Validate();
        if (serviceEvent.Source.ProducingHostId is null)
            throw new InvalidOperationException("DatabaseBackup service events require an authorized producing host.");

        return serviceEvent switch
        {
            DatabaseBackupServiceAcceptedEvent or DatabaseBackupServiceRejectedEvent
                or DatabaseRestoreServiceAcceptedEvent or DatabaseRestoreServiceRejectedEvent
                => Build<RecordDatabaseOperationAdmissionCommand>(serviceEvent),
            DatabaseBackupServiceStartedEvent or DatabaseRestoreServiceStartedEvent
                => Build<RecordDatabaseOperationStartedCommand>(serviceEvent),
            DatabaseBackupServiceProgressEvent or DatabaseRestoreServiceProgressEvent
                => Build<RecordDatabaseOperationProgressCommand>(serviceEvent),
            DatabaseBackupBoundaryEstablishedEvent
                => Build<RecordDatabaseBackupBoundaryCommand>(serviceEvent),
            DatabaseBackupArtifactReplicaUpdatedEvent
                => Build<RecordDatabaseArtifactReplicaCommand>(serviceEvent),
            DatabaseBackupVerificationCompletedEvent or DatabaseRestoreValidationCompletedEvent
                => Build<RecordDatabaseOperationVerificationCommand>(serviceEvent),
            DatabaseBackupServiceErrorEvent or DatabaseRestoreServiceErrorEvent
                => Build<RecordDatabaseOperationErrorCommand>(serviceEvent),
            DatabaseRestoreReadyForCutoverEvent
                => Build<RecordDatabaseRestoreReadyForCutoverCommand>(serviceEvent),
            DatabaseBackupServiceCompletedEvent or DatabaseRestoreServiceCompletedEvent or DatabaseRestoreDrillCompletedEvent
                => Build<CompleteDatabaseOperationCommand>(serviceEvent),
            DatabaseBackupServiceFailedEvent or DatabaseRestoreServiceFailedEvent
                => Build<FailDatabaseOperationCommand>(serviceEvent),
            DatabaseBackupServiceCancelledEvent or DatabaseRestoreServiceCancelledEvent
                => Build<RecordDatabaseOperationCancelledCommand>(serviceEvent),
            DatabaseRecoveryRunStatisticsCapturedEvent
                => Build<RecordDatabaseRecoveryRunStatisticsCommand>(serviceEvent),
            DatabaseBackupPolicyAppliedEvent or DatabaseBackupPolicyRejectedEvent
                => Build<RecordDatabaseBackupPolicyStatusCommand>(serviceEvent),
            DatabaseRetentionPlanCreatedEvent or DatabaseRetentionExecutionCompletedEvent or DatabaseRetentionExecutionFailedEvent
                => Build<RecordDatabaseRetentionResultCommand>(serviceEvent),
            DatabaseBackupServiceReconciliationEvent
                => Build<ReconcileDatabaseBackupServiceStateCommand>(serviceEvent),
            DatabaseBackupServiceCapabilityChangedEvent
                => Build<RecordDatabaseBackupServiceCapabilityCommand>(serviceEvent),
            _ => throw new InvalidOperationException($"No DatabaseBackup translation is registered for '{serviceEvent.GetType().Name}'.")
        };
    }

    /// <summary>Recovers a missing terminal retention display from a trusted service observation without execution.</summary>
    public static ReconcileDatabaseBackupServiceStateCommand ReconcileTerminalRetention(DatabaseBackupServiceEventContract serviceEvent)
    {
        _ = Translate(serviceEvent); // Apply the same envelope and producing-host validation.
        if (serviceEvent.Source.Source != BackupSource.AwsCloud
            || serviceEvent.Source.OperationKind != DatabaseRecoveryOperationKind.Retention
            || serviceEvent.Source.Phase is not (DatabaseRecoveryPhase.Completed or DatabaseRecoveryPhase.Failed))
            throw new InvalidOperationException("Only terminal AWS retention observations can recover missing retention display state.");
        var id = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            "restored-retention-display:v2:" + serviceEvent.Source.SourceEventId)).AsSpan(0, 16));
        return Build<ReconcileDatabaseBackupServiceStateCommand>(serviceEvent) with
        {
            CommandId = id,
            Source = serviceEvent.Source with { SourceEventId = id }
        };
    }

    static TCommand Build<TCommand>(DatabaseBackupServiceEventContract source)
        where TCommand : DatabaseBackupInternalCommand, new()
    {
        var template = new TCommand();
        return (template with
        {
            CommandId = source.Source.SourceEventId,
            EntityId = source.Source.OperationId,
            ErrorCode = 9190,
            Subject = new ActorSubject(ActorType.Command, DatabaseBackupCommandRoute.Actor, template.Verb, source.Source.OperationId.Format()),
            Source = source.Source,
            ProgressPercent = source.ProgressPercent,
            SafeDiagnosticReference = source.SafeDiagnosticReference,
            ArtifactReplica = source.ArtifactReplica,
            VerificationLevel = source.VerificationLevel,
            Outcome = source.Outcome,
            ErrorClassification = source.ErrorClassification,
            CutoverState = source.CutoverState,
            CapabilityState = source.CapabilityState,
            Statistics = source.Statistics,
            ExpectedStateRevision = source.ExpectedStateRevision,
            ValidationRevision = source.ValidationRevision,
            RestorePointId = source.RestorePointId,
            FreshTarget = source.FreshTarget,
            RestoreClass = source.RestoreClass,
            PolicyId = source.PolicyId,
            Policy = source.Policy,
            RetentionPlanId = source.RetentionPlanId,
            RetentionPlanRevision = source.RetentionPlanRevision,
            EvaluationBoundaryUtc = source.EvaluationBoundaryUtc,
            ManifestRevision = source.ManifestRevision,
            BackupLineage = source.BackupLineage
        });
    }
}
