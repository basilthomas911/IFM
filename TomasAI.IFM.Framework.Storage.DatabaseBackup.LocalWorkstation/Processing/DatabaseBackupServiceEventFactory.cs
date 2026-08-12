using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

internal static class DatabaseBackupServiceEventFactory
{
    public static DatabaseBackupServiceEventContract Accepted(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId)
        => intent.ExecutionEvent.Source.OperationKind switch
        {
            DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill
                => Create<DatabaseRestoreServiceAcceptedEvent>(intent, hostId, 1, DatabaseRecoveryPhase.Admitted),
            _ => Create<DatabaseBackupServiceAcceptedEvent>(intent, hostId, 1, DatabaseRecoveryPhase.Admitted)
        };

    public static DatabaseBackupServiceEventContract Started(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence)
        => intent.ExecutionEvent.Source.OperationKind switch
        {
            DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill
                => Create<DatabaseRestoreServiceStartedEvent>(intent, hostId, sequence, DatabaseRecoveryPhase.Started),
            _ => Create<DatabaseBackupServiceStartedEvent>(intent, hostId, sequence, DatabaseRecoveryPhase.Started)
        };

    public static DatabaseBackupBoundaryEstablishedEvent Boundary(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence,
        string safeReference)
        => Create<DatabaseBackupBoundaryEstablishedEvent>(
            intent, hostId, sequence, DatabaseRecoveryPhase.Capturing,
            safeDiagnosticReference: safeReference);

    public static DatabaseBackupVerificationCompletedEvent Verified(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence,
        DatabaseVerificationLevel level)
        => Create<DatabaseBackupVerificationCompletedEvent>(
            intent, hostId, sequence, DatabaseRecoveryPhase.Verifying,
            verificationLevel: level);

    public static DatabaseBackupArtifactReplicaUpdatedEvent ReplicaPublished(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence,
        DatabaseArtifactReplicaDescriptor replica,
        DatabaseRestorePointId restorePointId,
        long manifestRevision)
        => Create<DatabaseBackupArtifactReplicaUpdatedEvent>(
            intent, hostId, sequence, DatabaseRecoveryPhase.Transferring,
            artifactReplica: replica,
            restorePointId: restorePointId,
            manifestRevision: manifestRevision);

    public static DatabaseRestoreValidationCompletedEvent RestoreValidated(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence,
        string safeTargetReference,
        long validationRevision)
        => Create<DatabaseRestoreValidationCompletedEvent>(
            intent, hostId, sequence, DatabaseRecoveryPhase.Validating,
            safeDiagnosticReference: safeTargetReference,
            verificationLevel: DatabaseVerificationLevel.ApplicationValidation,
            validationRevision: validationRevision);

    public static DatabaseRecoveryRunStatisticsCapturedEvent Statistics(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence,
        DatabaseRecoveryPhase phase,
        DatabaseRecoveryRunStatistics statistics)
        => Create<DatabaseRecoveryRunStatisticsCapturedEvent>(
            intent, hostId, sequence, phase, statistics: statistics);

    public static DatabaseRestoreReadyForCutoverEvent ReadyForCutover(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence,
        string safeTargetReference,
        long validationRevision)
        => Create<DatabaseRestoreReadyForCutoverEvent>(
            intent, hostId, sequence, DatabaseRecoveryPhase.ReadyForCutover,
            safeDiagnosticReference: safeTargetReference,
            validationRevision: validationRevision);

    public static DatabaseBackupServiceEventContract Completed(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence)
        => intent.ExecutionEvent.Source.OperationKind switch
        {
            DatabaseRecoveryOperationKind.RestoreDrill
                => Create<DatabaseRestoreDrillCompletedEvent>(intent, hostId, sequence, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded),
            DatabaseRecoveryOperationKind.Restore
                => Create<DatabaseRestoreServiceCompletedEvent>(intent, hostId, sequence, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded),
            _ => Create<DatabaseBackupServiceCompletedEvent>(intent, hostId, sequence, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded)
        };

    public static TEvent Create<TEvent>(
        DatabaseExecutionIntent intent,
        DatabaseBackupHostId hostId,
        long sequence,
        DatabaseRecoveryPhase phase,
        DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None,
        string safeDiagnosticReference = "",
        DatabaseVerificationLevel verificationLevel = DatabaseVerificationLevel.None,
        DatabaseRecoveryRunStatistics? statistics = null,
        long validationRevision = 0,
        DatabaseArtifactReplicaDescriptor? artifactReplica = null,
        DatabaseRestorePointId? restorePointId = null,
        long manifestRevision = 0)
        where TEvent : DatabaseBackupServiceEventContract, new()
    {
        var execution = intent.ExecutionEvent;
        var id = DeterministicEventId(execution.Source.OperationId, sequence);
        var source = execution.Source with
        {
            SourceEventId = id,
            Phase = phase,
            ProducingHostId = hostId,
            SourceRevisionOrSequence = sequence,
            ObservedUtc = DateTimeOffset.UtcNow
        };
        var template = new TEvent();
        return (TEvent)(template with
        {
            Subject = new ActorSubject(ActorType.Event, "DatabaseBackupEvent", template.Verb, execution.EntityId.Format()),
            Id = id,
            EntityId = execution.EntityId,
            CommandId = execution.CommandId,
            AggregateId = execution.AggregateId,
            EventSource = "DatabaseBackupHost",
            ReceivedOn = source.ObservedUtc.UtcDateTime,
            Source = source,
            Request = execution.Request,
            Outcome = outcome,
            SafeDiagnosticReference = safeDiagnosticReference,
            VerificationLevel = verificationLevel,
            Statistics = statistics,
            ArtifactReplica = artifactReplica,
            RestorePointId = restorePointId ?? execution.RestorePointId,
            FreshTarget = execution.FreshTarget,
            RestoreClass = execution.RestoreClass,
            PolicyId = execution.PolicyId,
            Policy = execution.Policy,
            RequiredDestinations = execution.RequiredDestinations,
            ValidationRevision = validationRevision == 0 ? execution.ValidationRevision : validationRevision,
            RetentionPlanId = execution.RetentionPlanId,
            RetentionPlanRevision = execution.RetentionPlanRevision,
            EvaluationBoundaryUtc = execution.EvaluationBoundaryUtc,
            ManifestRevision = manifestRevision == 0 ? execution.ManifestRevision : manifestRevision
        });
    }

    static Guid DeterministicEventId(DatabaseRecoveryOperationId operationId, long sequence)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId.Value:N}:{sequence}"));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
