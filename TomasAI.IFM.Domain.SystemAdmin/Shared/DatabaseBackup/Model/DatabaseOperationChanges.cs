using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
/// <summary>Builds immutable source facts and stable service-observation fingerprints.</summary>
internal static class DatabaseOperationChanges
{
    internal static DatabaseSourceEnvelope Source(IDatabaseBackupCommand command, DatabaseBackupOperationState operation, DatabaseRecoveryOperationKind kind, DatabaseRecoveryPhase phase) => new()
    {
        SourceEventId = Guid.NewGuid(),
        OperationId = command.EntityId,
        BackupSetId = command.BackupSetId,
        Source = command.Source == BackupSource.None ? operation.Source : command.Source,
        ProtectionSetId = command.ProtectionSetId ?? (operation.Exists ? operation.ProtectionSetId : new DatabaseProtectionSetId("all")),
        PolicyRevision = command.ExpectedPolicyRevision == 0 ? operation.PolicyRevision : command.ExpectedPolicyRevision,
        OperationKind = kind,
        Phase = phase,
        SourceRevisionOrSequence = 0,
        CorrelationId = command.Request.CorrelationId,
        CausationId = command.Request.CausationId,
        ObservedUtc = command.Request.CreatedUtc
    };
    internal static DatabaseSourceEnvelope NextSource(DatabaseSourceEnvelope source, DatabaseRecoveryPhase phase) => source with
    {
        SourceEventId = Guid.NewGuid(),
        Phase = phase
    };
    internal static DatabaseBackupChange CreateChange<TEvent>(IDatabaseBackupCommand command, DatabaseSourceEnvelope source, DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None)
        where TEvent : DatabaseBackupEventContract, new()
    {
        return new DatabaseBackupChange
        {
            EventFamily = typeof(TEvent),
            DatabaseSource = source,
            DatabaseRequest = command.Request,
            Outcome = outcome,
            RestorePointId = command.RestorePointId,
            FreshTarget = command.FreshTarget,
            BackupPolicy = command.Policy,
            RequiredDestinations = command.RequiredDestinations,
            ValidationRevision = command.ValidationRevision,
            RetentionPlanId = command.RetentionPlanId,
            RetentionPlanRevision = command.RetentionPlanRevision,
            RestoreClass = command.RestoreClass,
            EvaluationBoundaryUtc = command.EvaluationBoundaryUtc,
            PolicyId = command.PolicyId,
            ManifestRevision = command.ExpectedManifestRevision,
            BackupLineage = (command is RequestDatabaseBackupCommand) switch
            {
                true => new DatabaseBackupLineage
                {
                    RequestedMode = command.RequestedBackupMode == DatabaseBackupMode.None ? DatabaseBackupMode.Full : command.RequestedBackupMode
                },
                false => null
            }
        };
    }

    internal static DatabaseBackupChange CreateChange<TEvent>(DatabaseBackupInternalCommand command, DatabaseRecoveryPhase phase, DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None)
        where TEvent : DatabaseBackupEventContract, new()
    {
        var source = command.Source with
        {
            Phase = phase
        };
        return new DatabaseBackupChange
        {
            EventFamily = typeof(TEvent),
            DatabaseSource = source,
            ProgressPercent = command.ProgressPercent,
            SafeDiagnosticReference = command.SafeDiagnosticReference,
            ArtifactReplica = command.ArtifactReplica,
            VerificationLevel = command.VerificationLevel,
            Outcome = outcome == DatabaseRecoveryOutcome.None ? command.Outcome : outcome,
            ErrorClassification = command.ErrorClassification,
            CutoverState = command.CutoverState,
            CapabilityState = command.CapabilityState,
            Statistics = command.Statistics,
            ValidationRevision = command.ValidationRevision,
            RestorePointId = command.RestorePointId,
            FreshTarget = command.FreshTarget,
            RestoreClass = command.RestoreClass,
            PolicyId = command.PolicyId,
            BackupPolicy = command.Policy,
            RetentionPlanId = command.RetentionPlanId,
            RetentionPlanRevision = command.RetentionPlanRevision,
            EvaluationBoundaryUtc = command.EvaluationBoundaryUtc,
            ManifestRevision = command.ManifestRevision,
            BackupLineage = command.BackupLineage
        };
    }

internal static string Fingerprint(DatabaseBackupChange backupChange) => JsonSerializer.Serialize(new { Type = backupChange.EventFamily.FullName, Source = backupChange.DatabaseSource, backupChange.ProgressPercent, backupChange.SafeDiagnosticReference, backupChange.ArtifactReplica, backupChange.VerificationLevel, backupChange.Outcome, backupChange.ErrorClassification, backupChange.CutoverState, backupChange.CapabilityState, backupChange.Statistics, backupChange.RestorePointId, backupChange.FreshTarget, Policy = backupChange.BackupPolicy, backupChange.RequiredDestinations, backupChange.ValidationRevision, backupChange.RetentionPlanId, backupChange.RetentionPlanRevision, backupChange.RestoreClass, backupChange.EvaluationBoundaryUtc, backupChange.PolicyId, backupChange.ManifestRevision, backupChange.BackupLineage });
}
