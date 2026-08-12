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

    static TCommand Build<TCommand>(DatabaseBackupServiceEventContract source)
        where TCommand : DatabaseBackupInternalCommand, new()
    {
        var template = new TCommand();
        return (TCommand)(template with
        {
            CommandId = source.Source.SourceEventId,
            EntityId = source.Source.OperationId,
            ErrorCode = 9190,
            Subject = new ActorSubject(ActorType.Command, DatabaseBackupCommand.Actor, template.Verb, source.Source.OperationId.Format()),
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
            ManifestRevision = source.ManifestRevision
        });
    }
}
