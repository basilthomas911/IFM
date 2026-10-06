using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Model;
/// <summary>Converts accepted immutable changes into the permanent source-event schemas.</summary>
internal static class DatabaseBackupEventFactory
{
    /// <summary>Creates each ordered event with the originating command ID before application.</summary>
    /// <param name="command">The originating operator or service command.</param>
    /// <param name="transition">The accepted ordered business changes.</param>
    /// <returns>The ordered source-event batch.</returns>
    internal static IReadOnlyList<DatabaseBackupEventContract> Create(ICommand command, DatabaseBackupTransition transition)
        => transition.LifecycleChanges.Select(change => Create(command, change)).ToArray();
    /// <summary>Creates one permanent source-event schema from accepted business information.</summary>
    /// <param name="command">The originating command.</param>
    /// <param name="change">The accepted lifecycle change.</param>
    /// <returns>The source event with explicit command identity.</returns>
    static DatabaseBackupEventContract Create(ICommand command, DatabaseBackupChange change)
    {
        var template = (DatabaseBackupEventContract)Activator.CreateInstance(change.EventFamily)!;
        return template with
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, "DatabaseBackupEvent", template.Verb, change.DatabaseSource.OperationId.Format()),
            Id = change.DatabaseSource.SourceEventId,
            EntityId = change.DatabaseSource.OperationId,
            AggregateId = change.DatabaseSource.OperationId.Format(),
            EventSource = command is DatabaseBackupInternalCommand ? DatabaseBackupInternalCommand.Actor : DatabaseBackupCommandRoute.Actor,
            ReceivedOn = change.DatabaseSource.ObservedUtc.UtcDateTime,
            Source = change.DatabaseSource,
            Request = change.DatabaseRequest,
            ProgressPercent = change.ProgressPercent,
            SafeDiagnosticReference = change.SafeDiagnosticReference,
            ArtifactReplica = change.ArtifactReplica,
            VerificationLevel = change.VerificationLevel,
            Outcome = change.Outcome,
            ErrorClassification = change.ErrorClassification,
            CutoverState = change.CutoverState,
            CapabilityState = change.CapabilityState,
            Statistics = change.Statistics,
            RestorePointId = change.RestorePointId,
            FreshTarget = change.FreshTarget,
            Policy = change.BackupPolicy,
            RequiredDestinations = change.RequiredDestinations,
            ExpectedStateRevision = change.ExpectedStateRevision,
            ValidationRevision = change.ValidationRevision,
            RetentionPlanId = change.RetentionPlanId,
            RetentionPlanRevision = change.RetentionPlanRevision,
            RestoreClass = change.RestoreClass,
            EvaluationBoundaryUtc = change.EvaluationBoundaryUtc,
            PolicyId = change.PolicyId,
            ManifestRevision = change.ManifestRevision,
            BackupLineage = change.BackupLineage,
        };
    }
    /// <summary>Describes committed events using the identical duplicate-observation fingerprint inputs.</summary>
    /// <param name="sourceEvent">The committed lifecycle event.</param>
    /// <returns>Its immutable business description.</returns>
    internal static DatabaseBackupChange Describe(DatabaseBackupEventContract sourceEvent)
        => new()
        {
            EventFamily = sourceEvent.GetType(),
            DatabaseSource = sourceEvent.Source,
            DatabaseRequest = sourceEvent.Request,
            ProgressPercent = sourceEvent.ProgressPercent,
            SafeDiagnosticReference = sourceEvent.SafeDiagnosticReference,
            ArtifactReplica = sourceEvent.ArtifactReplica,
            VerificationLevel = sourceEvent.VerificationLevel,
            Outcome = sourceEvent.Outcome,
            ErrorClassification = sourceEvent.ErrorClassification,
            CutoverState = sourceEvent.CutoverState,
            CapabilityState = sourceEvent.CapabilityState,
            Statistics = sourceEvent.Statistics,
            RestorePointId = sourceEvent.RestorePointId,
            FreshTarget = sourceEvent.FreshTarget,
            BackupPolicy = sourceEvent.Policy,
            RequiredDestinations = sourceEvent.RequiredDestinations,
            ExpectedStateRevision = sourceEvent.ExpectedStateRevision,
            ValidationRevision = sourceEvent.ValidationRevision,
            RetentionPlanId = sourceEvent.RetentionPlanId,
            RetentionPlanRevision = sourceEvent.RetentionPlanRevision,
            RestoreClass = sourceEvent.RestoreClass,
            EvaluationBoundaryUtc = sourceEvent.EvaluationBoundaryUtc,
            PolicyId = sourceEvent.PolicyId,
            ManifestRevision = sourceEvent.ManifestRevision,
            BackupLineage = sourceEvent.BackupLineage,
        };
}
