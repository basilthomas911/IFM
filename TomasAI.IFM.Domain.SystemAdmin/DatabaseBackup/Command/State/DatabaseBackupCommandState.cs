using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using System.Text.Json;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;

public sealed class DatabaseBackupCommandState
    : BaseEventSourceActorState<DatabaseBackupCommandState>, IEventSourceActorState<DatabaseBackupCommandState>
{
    readonly Dictionary<Guid, string> _processedServiceEvents = [];

    public override ActorThreadId Id { get; set; } = default!;
    public DatabaseBackupOperationState Operation { get; private set; } = new();
    public DatabaseRestoreOperationState Restore { get; private set; } = new();
    public DatabaseBackupSetState BackupSet { get; private set; } = new();
    public DatabaseBackupPolicyState Policy { get; private set; } = new();
    public DatabaseBackupServiceState Service { get; private set; } = new();
    public DatabaseRetentionState Retention { get; private set; } = new();

    public DatabaseRecoveryOperationId Execute(DatabaseBackupCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureExpectedRevision(command.ExpectedStateRevision);
        return command switch
        {
            RequestDatabaseBackupCommand value => RequestBackup(value),
            CancelDatabaseBackupCommand value => Cancel(value),
            RequestDatabaseRestoreCommand value => RequestRestore(value),
            ApproveDatabaseRestoreCommand value => ApproveRestore(value),
            CancelDatabaseRestoreCommand value => Cancel(value),
            ApproveDatabaseCutoverCommand value => ApproveCutover(value),
            RequestDatabaseRestoreDrillCommand value => RequestDrill(value),
            UpdateDatabaseBackupPolicyCommand value => UpdatePolicy(value),
            PlaceBackupLegalHoldCommand value => PlaceLegalHold(value),
            ReleaseBackupLegalHoldCommand value => ReleaseLegalHold(value),
            RequestBackupRetentionEvaluationCommand value => RequestRetention(value),
            ExecuteBackupRetentionPlanCommand value => ExecuteRetention(value),
            _ => throw new InvalidOperationException($"Unsupported DatabaseBackup command '{command.GetType().Name}'.")
        };
    }

    public DatabaseRecoveryOperationId Execute(DatabaseBackupInternalCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureExpectedRevision(command.ExpectedStateRevision);
        var domainEvent = Translate(command);
        var fingerprint = Fingerprint(domainEvent);
        if (_processedServiceEvents.TryGetValue(command.Source.SourceEventId, out var existing))
        {
            if (!StringComparer.Ordinal.Equals(existing, fingerprint))
                throw new InvalidOperationException("A service event ID was replayed with conflicting content.");
            return Operation.OperationId;
        }
        EnsureServiceIdentityAndSequence(command.Source);
        EnsureLegalTransition(domainEvent.Source.Phase, domainEvent.Outcome);
        Update(domainEvent, command);
        return Operation.OperationId;
    }

    static DatabaseBackupEventContract Translate(DatabaseBackupInternalCommand command)
        => command switch
        {
            RecordDatabaseOperationAdmissionCommand => Create<DatabaseOperationAdmissionRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseOperationStartedCommand => Create<DatabaseOperationStartedEvent>(command, DatabaseRecoveryPhase.Started),
            RecordDatabaseOperationProgressCommand => Create<DatabaseOperationProgressRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseBackupBoundaryCommand => Create<DatabaseBackupBoundaryRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseArtifactReplicaCommand => Create<DatabaseArtifactReplicaRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseOperationVerificationCommand => command.Source.OperationKind is DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill
                ? Create<DatabaseRestoreValidationRecordedEvent>(command, DatabaseRecoveryPhase.Validating)
                : Create<DatabaseOperationVerificationRecordedEvent>(command, DatabaseRecoveryPhase.Verifying),
            RecordDatabaseOperationErrorCommand => Create<DatabaseOperationErrorRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseRestoreReadyForCutoverCommand => Create<DatabaseRestoreReadyForCutoverRecordedEvent>(command, DatabaseRecoveryPhase.ReadyForCutover),
            CompleteDatabaseOperationCommand => Create<DatabaseOperationCompletedEvent>(command, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded),
            FailDatabaseOperationCommand => Create<DatabaseOperationFailedEvent>(command, DatabaseRecoveryPhase.Failed, DatabaseRecoveryOutcome.Failed),
            RecordDatabaseOperationCancelledCommand => Create<DatabaseOperationCancelledEvent>(command, DatabaseRecoveryPhase.Cancelled, DatabaseRecoveryOutcome.Cancelled),
            RecordDatabaseBackupPolicyStatusCommand => Create<DatabaseBackupPolicyEnforcedEvent>(command, command.Source.Phase),
            RecordDatabaseRetentionResultCommand => Create<DatabaseRetentionExecutionRequestedDomainEvent>(command, command.Source.Phase, command.Outcome),
            ReconcileDatabaseBackupServiceStateCommand => Create<DatabaseBackupServiceReconciledEvent>(command, command.Source.Phase),
            RecordDatabaseBackupServiceCapabilityCommand => Create<DatabaseBackupServiceCapabilityRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseRecoveryRunStatisticsCommand => Create<DatabaseRecoveryStatisticsRecordedEvent>(command, command.Source.Phase),
            _ => throw new InvalidOperationException($"Unsupported internal DatabaseBackup command '{command.GetType().Name}'.")
        };

    DatabaseRecoveryOperationId RequestBackup(RequestDatabaseBackupCommand command)
    {
        EnsureNewOperation(command.EntityId);
        var source = Source(command, DatabaseRecoveryOperationKind.Backup, DatabaseRecoveryPhase.Requested);
        Update(Create<DatabaseBackupRequestedDomainEvent>(command, source), command);
        Update(Create<DatabaseBackupAuthorizedDomainEvent>(command, source with { Phase = DatabaseRecoveryPhase.Authorized }), command);
        Update(Create<DatabaseBackupExecutionRequestedDomainEvent>(command, source with { Phase = DatabaseRecoveryPhase.Requested }), command);
        return command.EntityId;
    }

    DatabaseRecoveryOperationId RequestRestore(RequestDatabaseRestoreCommand command)
    {
        EnsureNewOperation(command.EntityId);
        var source = Source(command, DatabaseRecoveryOperationKind.Restore, DatabaseRecoveryPhase.Requested);
        Update(Create<DatabaseRestoreRequestedDomainEvent>(command, source), command);
        return command.EntityId;
    }

    DatabaseRecoveryOperationId ApproveRestore(ApproveDatabaseRestoreCommand command)
    {
        EnsureOperation(DatabaseRecoveryOperationKind.Restore, DatabaseRecoveryPhase.Requested);
        var source = Source(command, Operation.Kind, DatabaseRecoveryPhase.Authorized);
        Update(Create<DatabaseRestoreAuthorizedDomainEvent>(command, source), command);
        Update(Create<DatabaseRestoreExecutionRequestedDomainEvent>(command, source with { Phase = DatabaseRecoveryPhase.Requested }), command);
        return Operation.OperationId;
    }

    DatabaseRecoveryOperationId RequestDrill(RequestDatabaseRestoreDrillCommand command)
    {
        EnsureNewOperation(command.EntityId);
        var source = Source(command, DatabaseRecoveryOperationKind.RestoreDrill, DatabaseRecoveryPhase.Requested);
        Update(Create<DatabaseRestoreDrillRequestedDomainEvent>(command, source), command);
        Update(Create<DatabaseRestoreDrillAuthorizedDomainEvent>(command, source with { Phase = DatabaseRecoveryPhase.Authorized }), command);
        Update(Create<DatabaseRestoreDrillExecutionRequestedDomainEvent>(command, source with { Phase = DatabaseRecoveryPhase.Requested }), command);
        return command.EntityId;
    }

    DatabaseRecoveryOperationId ApproveCutover(ApproveDatabaseCutoverCommand command)
    {
        EnsureOperation(DatabaseRecoveryOperationKind.Restore, DatabaseRecoveryPhase.ReadyForCutover);
        if (command.ValidationRevision != Operation.ValidationRevision)
            throw new InvalidOperationException("Cutover approval does not match the current validation revision.");
        var source = Source(command, DatabaseRecoveryOperationKind.Cutover, DatabaseRecoveryPhase.Authorized);
        Update(Create<DatabaseCutoverRequestedDomainEvent>(command, source), command);
        Update(Create<DatabaseCutoverAuthorizedDomainEvent>(command, source), command);
        Update(Create<DatabaseCutoverExecutionRequestedDomainEvent>(command, source with { Phase = DatabaseRecoveryPhase.CuttingOver }), command);
        return Operation.OperationId;
    }

    DatabaseRecoveryOperationId Cancel(DatabaseBackupCommand command)
    {
        if (!Operation.Exists || Operation.IsTerminal) throw new InvalidOperationException("Only an active operation can be cancelled.");
        var source = Source(command, Operation.Kind, DatabaseRecoveryPhase.Cancelled);
        Update(Create<DatabaseOperationCancelledEvent>(command, source, DatabaseRecoveryOutcome.Cancelled), command);
        return Operation.OperationId;
    }

    DatabaseRecoveryOperationId UpdatePolicy(UpdateDatabaseBackupPolicyCommand command)
    {
        var source = Source(command, DatabaseRecoveryOperationKind.Reconciliation, DatabaseRecoveryPhase.Authorized);
        Update(Create<DatabaseBackupPolicyRevisedEvent>(command, source), command);
        Update(Create<DatabaseBackupPolicyEnforcedEvent>(command, source), command);
        return command.EntityId;
    }

    DatabaseRecoveryOperationId PlaceLegalHold(PlaceBackupLegalHoldCommand command)
    {
        var source = Source(command, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Authorized);
        Update(Create<DatabaseBackupLegalHoldPlacedEvent>(command, source), command);
        return command.EntityId;
    }

    DatabaseRecoveryOperationId ReleaseLegalHold(ReleaseBackupLegalHoldCommand command)
    {
        var source = Source(command, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Authorized);
        Update(Create<DatabaseBackupLegalHoldReleasedEvent>(command, source), command);
        return command.EntityId;
    }

    DatabaseRecoveryOperationId RequestRetention(RequestBackupRetentionEvaluationCommand command)
    {
        EnsureNewOperation(command.EntityId);
        var source = Source(command, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Requested);
        Update(Create<DatabaseRetentionRequestedDomainEvent>(command, source), command);
        Update(Create<DatabaseRetentionAuthorizedDomainEvent>(command, source with { Phase = DatabaseRecoveryPhase.Authorized }), command);
        return command.EntityId;
    }

    DatabaseRecoveryOperationId ExecuteRetention(ExecuteBackupRetentionPlanCommand command)
    {
        var source = Source(command, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Requested);
        Update(Create<DatabaseRetentionExecutionRequestedDomainEvent>(command, source), command);
        return command.EntityId;
    }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not DatabaseBackupEventContract e
            || e.GetType().Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) != true)
            return false;

        var source = e.Source;
        var nextRevision = Operation.Revision + 1;
        var phase = e switch
        {
            DatabaseOperationCompletedEvent => DatabaseRecoveryPhase.Completed,
            DatabaseOperationFailedEvent => DatabaseRecoveryPhase.Failed,
            DatabaseOperationCancelledEvent => DatabaseRecoveryPhase.Cancelled,
            _ => source.Phase
        };
        var outcome = e switch
        {
            DatabaseOperationCompletedEvent => DatabaseRecoveryOutcome.Succeeded,
            DatabaseOperationFailedEvent => DatabaseRecoveryOutcome.Failed,
            DatabaseOperationCancelledEvent => DatabaseRecoveryOutcome.Cancelled,
            _ => e.Outcome
        };
        Operation = Operation with
        {
            OperationId = source.OperationId,
            BackupSetId = source.BackupSetId,
            ProtectionSetId = source.ProtectionSetId,
            Source = source.Source,
            Kind = source.OperationKind,
            Phase = phase,
            Outcome = outcome,
            PolicyRevision = source.PolicyRevision,
            Revision = nextRevision,
            ProgressPercent = e.ProgressPercent,
            HostId = source.ProducingHostId ?? Operation.HostId,
            LastServiceSequence = source.ProducingHostId is null ? Operation.LastServiceSequence : source.SourceRevisionOrSequence,
            ValidationRevision = e.ValidationRevision == 0 ? Operation.ValidationRevision : e.ValidationRevision,
            CutoverState = e.CutoverState == DatabaseCutoverState.None ? Operation.CutoverState : e.CutoverState,
            BackupLineage = e.BackupLineage ?? Operation.BackupLineage
        };
        if (source.ProducingHostId is not null)
        {
            _processedServiceEvents[source.SourceEventId] = Fingerprint(e);
            Service = Service with
            {
                Source = source.Source,
                HostId = source.ProducingHostId,
                LastServiceSequence = source.SourceRevisionOrSequence,
                CapabilityState = e.CapabilityState == DatabaseServiceCapabilityState.None ? Service.CapabilityState : e.CapabilityState,
                Reconciled = e is DatabaseBackupServiceReconciledEvent || Service.Reconciled
            };
        }
        if (e.RestorePointId is not null || source.OperationKind is DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill)
            Restore = Restore with { RestorePointId = e.RestorePointId ?? Restore.RestorePointId, CutoverState = e.CutoverState == DatabaseCutoverState.None ? Restore.CutoverState : e.CutoverState };
        if (e.Policy is not null)
            Policy = Policy with { Definition = e.Policy, Revision = source.PolicyRevision, Enforced = e is DatabaseBackupPolicyEnforcedEvent };
        if (e.RetentionPlanId is not null)
            Retention = Retention with { PlanId = e.RetentionPlanId, PlanRevision = e.RetentionPlanRevision, Outcome = e.Outcome, Revision = Retention.Revision + 1 };
        if (e is DatabaseBackupSetCheckpointRecordedEvent or DatabaseBackupSetCompletedEvent)
            BackupSet = BackupSet with { BackupSetId = source.BackupSetId, CheckpointCount = BackupSet.CheckpointCount + 1, Complete = e is DatabaseBackupSetCompletedEvent, Revision = BackupSet.Revision + 1 };
        return true;
    }

    void EnsureNewOperation(DatabaseRecoveryOperationId operationId)
    {
        if (Operation.Exists && Operation.OperationId != operationId) throw new InvalidOperationException("The aggregate already owns another operation.");
        if (Operation.Exists) throw new InvalidOperationException("The operation was already requested.");
    }

    void EnsureOperation(DatabaseRecoveryOperationKind kind, DatabaseRecoveryPhase phase)
    {
        if (!Operation.Exists || Operation.Kind != kind || Operation.Phase != phase)
            throw new InvalidOperationException($"Operation must be {kind} in {phase} phase.");
    }

    void EnsureExpectedRevision(long expected)
    {
        if (expected > 0 && expected != Operation.Revision)
            throw new InvalidOperationException($"Expected revision {expected} does not match current revision {Operation.Revision}.");
    }

    void EnsureServiceIdentityAndSequence(DatabaseSourceEnvelope source)
    {
        if (!Operation.Exists) throw new InvalidOperationException("Service observations require an existing operation.");
        if (source.OperationId != Operation.OperationId || source.Source != Operation.Source || source.ProtectionSetId != Operation.ProtectionSetId
            || source.OperationKind != Operation.Kind)
            throw new InvalidOperationException("Service observation does not match the immutable operation definition.");
        if (Operation.HostId is not null && source.ProducingHostId != Operation.HostId)
            throw new InvalidOperationException("Producing host changed for an existing operation.");
        var expected = Operation.LastServiceSequence + 1;
        if (source.SourceRevisionOrSequence != expected)
            throw new InvalidOperationException($"Service sequence gap: expected {expected}, received {source.SourceRevisionOrSequence}.");
    }

    void EnsureLegalTransition(DatabaseRecoveryPhase target, DatabaseRecoveryOutcome outcome)
    {
        if (Operation.IsTerminal) throw new InvalidOperationException("Terminal operations cannot transition.");
        if (target == DatabaseRecoveryPhase.Started && Operation.Phase is not (DatabaseRecoveryPhase.Admitted or DatabaseRecoveryPhase.Preflight))
            throw new InvalidOperationException("Started requires admitted or preflight state.");
        if (target is DatabaseRecoveryPhase.Capturing or DatabaseRecoveryPhase.Transferring or DatabaseRecoveryPhase.Verifying or DatabaseRecoveryPhase.Validating
            && Operation.Phase is not (DatabaseRecoveryPhase.Started or DatabaseRecoveryPhase.Capturing or DatabaseRecoveryPhase.Transferring or DatabaseRecoveryPhase.Verifying or DatabaseRecoveryPhase.Validating))
            throw new InvalidOperationException("Progress requires a started operation.");
        if (target == DatabaseRecoveryPhase.Completed && outcome != DatabaseRecoveryOutcome.Succeeded)
            throw new InvalidOperationException("Completion requires a successful outcome.");
        if (target == DatabaseRecoveryPhase.Completed && Operation.Phase is DatabaseRecoveryPhase.Requested or DatabaseRecoveryPhase.Authorized or DatabaseRecoveryPhase.Admitted)
            throw new InvalidOperationException("An operation cannot complete before it starts.");
        if (target == DatabaseRecoveryPhase.ReadyForCutover && (Operation.Kind != DatabaseRecoveryOperationKind.Restore || Operation.Phase != DatabaseRecoveryPhase.Validating))
            throw new InvalidOperationException("Only production restore can become ready for cutover.");
    }

    DatabaseSourceEnvelope Source(DatabaseBackupCommand command, DatabaseRecoveryOperationKind kind, DatabaseRecoveryPhase phase)
        => new()
        {
            SourceEventId = Guid.NewGuid(), OperationId = command.EntityId, BackupSetId = command.BackupSetId,
            Source = command.Source == BackupSource.None ? Operation.Source : command.Source,
            ProtectionSetId = command.ProtectionSetId ?? (Operation.Exists ? Operation.ProtectionSetId : new DatabaseProtectionSetId("all")),
            PolicyRevision = command.ExpectedPolicyRevision == 0 ? Operation.PolicyRevision : command.ExpectedPolicyRevision,
            OperationKind = kind, Phase = phase, SourceRevisionOrSequence = 0,
            CorrelationId = command.Request.CorrelationId, CausationId = command.Request.CausationId,
            ObservedUtc = command.Request.CreatedUtc
        };

    static TEvent Create<TEvent>(DatabaseBackupCommand command, DatabaseSourceEnvelope source, DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None)
        where TEvent : DatabaseBackupEventContract, new()
    {
        var template = new TEvent();
        return (TEvent)(template with
        {
            Subject = new ActorSubject(ActorType.Event, "DatabaseBackupEvent", template.Verb, source.OperationId.Format()),
            Id = source.SourceEventId, EntityId = source.OperationId, CommandId = command.CommandId,
            AggregateId = source.OperationId.Format(), EventSource = DatabaseBackupCommand.Actor,
            ReceivedOn = source.ObservedUtc.UtcDateTime, Source = source, Request = command.Request,
            Outcome = outcome, RestorePointId = command.RestorePointId, FreshTarget = command.FreshTarget,
            Policy = command.Policy, RequiredDestinations = command.RequiredDestinations,
            ValidationRevision = command.ValidationRevision, RetentionPlanId = command.RetentionPlanId,
            RetentionPlanRevision = command.RetentionPlanRevision, RestoreClass = command.RestoreClass,
            EvaluationBoundaryUtc = command.EvaluationBoundaryUtc, PolicyId = command.PolicyId,
            ManifestRevision = command.ExpectedManifestRevision,
            BackupLineage = command is RequestDatabaseBackupCommand
                ? new DatabaseBackupLineage
                {
                    RequestedMode = command.RequestedBackupMode == DatabaseBackupMode.None
                        ? DatabaseBackupMode.Full
                        : command.RequestedBackupMode
                }
                : null
        });
    }

    static TEvent Create<TEvent>(DatabaseBackupInternalCommand command, DatabaseRecoveryPhase phase, DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None)
        where TEvent : DatabaseBackupEventContract, new()
    {
        var source = command.Source with { Phase = phase };
        var template = new TEvent();
        return (TEvent)(template with
        {
            Subject = new ActorSubject(ActorType.Event, "DatabaseBackupEvent", template.Verb, source.OperationId.Format()),
            Id = source.SourceEventId, EntityId = source.OperationId, CommandId = command.CommandId,
            AggregateId = source.OperationId.Format(), EventSource = DatabaseBackupInternalCommand.Actor,
            ReceivedOn = source.ObservedUtc.UtcDateTime, Source = source, ProgressPercent = command.ProgressPercent,
            SafeDiagnosticReference = command.SafeDiagnosticReference, ArtifactReplica = command.ArtifactReplica,
            VerificationLevel = command.VerificationLevel, Outcome = outcome == DatabaseRecoveryOutcome.None ? command.Outcome : outcome,
            ErrorClassification = command.ErrorClassification, CutoverState = command.CutoverState,
            CapabilityState = command.CapabilityState, Statistics = command.Statistics,
            ValidationRevision = command.ValidationRevision, RestorePointId = command.RestorePointId,
            FreshTarget = command.FreshTarget, RestoreClass = command.RestoreClass,
            PolicyId = command.PolicyId, Policy = command.Policy,
            RetentionPlanId = command.RetentionPlanId, RetentionPlanRevision = command.RetentionPlanRevision,
            EvaluationBoundaryUtc = command.EvaluationBoundaryUtc,
            ManifestRevision = command.ManifestRevision,
            BackupLineage = command.BackupLineage
        });
    }

    static string Fingerprint(DatabaseBackupEventContract domainEvent)
        => JsonSerializer.Serialize(new
        {
            Type = domainEvent.GetType().FullName,
            domainEvent.Source,
            domainEvent.ProgressPercent,
            domainEvent.SafeDiagnosticReference,
            domainEvent.ArtifactReplica,
            domainEvent.VerificationLevel,
            domainEvent.Outcome,
            domainEvent.ErrorClassification,
            domainEvent.CutoverState,
            domainEvent.CapabilityState,
            domainEvent.Statistics,
            domainEvent.RestorePointId,
            domainEvent.FreshTarget,
            domainEvent.Policy,
            domainEvent.RequiredDestinations,
            domainEvent.ValidationRevision,
            domainEvent.RetentionPlanId,
            domainEvent.RetentionPlanRevision,
            domainEvent.RestoreClass,
            domainEvent.EvaluationBoundaryUtc,
            domainEvent.PolicyId,
            domainEvent.ManifestRevision,
            domainEvent.BackupLineage
        });
}
