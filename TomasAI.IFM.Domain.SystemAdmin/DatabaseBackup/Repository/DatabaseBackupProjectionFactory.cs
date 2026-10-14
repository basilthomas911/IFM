using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Repository;
/// <summary>Translates authoritative backup events into accepted read-model facts.</summary>
public static class DatabaseBackupProjectionFactory
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>Validates the domain source and derives projection policy before database persistence.</summary>
    public static DatabaseBackupProjectionSnapshot Create(DatabaseBackupEventContract domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        domainEvent.Validate();
        if (domainEvent.EventId <= 0 || domainEvent.GetType().Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) != true)
            throw new ArgumentException("A persisted authoritative backup domain event is required.", nameof(domainEvent));
        var terminal = domainEvent.Source.Phase is DatabaseRecoveryPhase.Completed or DatabaseRecoveryPhase.Failed
            or DatabaseRecoveryPhase.Cancelled or DatabaseRecoveryPhase.Rejected;
        return new()
        {
            Id = domainEvent.Id,
            EventId = domainEvent.EventId,
            Source = domainEvent.Source,
            Request = domainEvent.Request,
            ProgressPercent = domainEvent.ProgressPercent,
            SafeDiagnosticReference = domainEvent.SafeDiagnosticReference,
            ArtifactReplica = domainEvent.ArtifactReplica,
            VerificationLevel = domainEvent.VerificationLevel,
            Outcome = domainEvent.Outcome,
            ErrorClassification = domainEvent.ErrorClassification,
            CutoverState = domainEvent.CutoverState,
            CapabilityState = domainEvent.CapabilityState,
            Statistics = domainEvent.Statistics,
            RestorePointId = domainEvent.RestorePointId,
            FreshTarget = domainEvent.FreshTarget,
            Policy = domainEvent.Policy,
            ValidationRevision = domainEvent.ValidationRevision,
            RetentionPlanId = domainEvent.RetentionPlanId,
            RetentionPlanRevision = domainEvent.RetentionPlanRevision,
            RestoreClass = domainEvent.RestoreClass,
            EvaluationBoundaryUtc = domainEvent.EvaluationBoundaryUtc == default ? domainEvent.Source.ObservedUtc : domainEvent.EvaluationBoundaryUtc,
            PolicyId = domainEvent.PolicyId,
            ManifestRevision = domainEvent.ManifestRevision,
            BackupLineage = domainEvent.BackupLineage,
            EventHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), JsonOptions)))),
            WriteOperation = domainEvent is not (DatabaseBackupPolicyRevisedEvent or DatabaseBackupPolicyEnforcedEvent
                or DatabaseBackupLegalHoldPlacedEvent or DatabaseBackupLegalHoldReleasedEvent
                or DatabaseBackupServiceCapabilityRecordedEvent or DatabaseBackupServiceReconciledEvent),
            WriteError = domainEvent is DatabaseOperationErrorRecordedEvent or DatabaseOperationFailedEvent,
            WriteServiceHealth = domainEvent.Source.ProducingHostId is not null
                && domainEvent is DatabaseBackupServiceCapabilityRecordedEvent or DatabaseBackupServiceReconciledEvent,
            Eligible = domainEvent.Outcome is not DatabaseRecoveryOutcome.Failed and not DatabaseRecoveryOutcome.Rejected,
            LegalHold = domainEvent is DatabaseBackupLegalHoldPlacedEvent,
            RestoreTested = domainEvent.Source.OperationKind == DatabaseRecoveryOperationKind.RestoreDrill && domainEvent is DatabaseOperationCompletedEvent,
            Enforced = domainEvent is DatabaseBackupPolicyEnforcedEvent,
            Reconciled = domainEvent is DatabaseBackupServiceReconciledEvent,
            Ready = domainEvent.CapabilityState == DatabaseServiceCapabilityState.Ready,
            Approved = domainEvent is DatabaseRetentionAuthorizedDomainEvent or DatabaseRetentionExecutionRequestedDomainEvent,
            CompletedUtc = terminal ? domainEvent.Source.ObservedUtc.UtcDateTime : null,
            VerifiedUtc = domainEvent.VerificationLevel == DatabaseVerificationLevel.None ? null : domainEvent.Source.ObservedUtc.UtcDateTime
        };
    }
}
