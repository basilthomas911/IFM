using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using System.Text.Json;

using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Model;
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

    /// <summary>Preserves legacy callers through pure computation and event application.</summary>
    /// <param name="command">The operator intent.</param>
    /// <returns>The existing recovery-operation identity.</returns>
    public DatabaseRecoveryOperationId Execute(IDatabaseBackupCommand command)
    {
        var transition = Compute(command);
        if (!Update(DatabaseBackupEventFactory.Create(command, transition), command)) throw new InvalidOperationException("DatabaseBackup.STATE.APPLY_FAILED");
        return command.EntityId;
    }
    /// <summary>Preserves legacy service-observation callers through the same application path.</summary>
    /// <param name="command">The service observation.</param>
    /// <returns>The existing recovery-operation identity.</returns>
    public DatabaseRecoveryOperationId Execute(DatabaseBackupInternalCommand command)
    {
        var transition = Compute(command);
        if (!Update(DatabaseBackupEventFactory.Create(command, transition), command)) throw new InvalidOperationException("DatabaseBackup.STATE.APPLY_FAILED");
        return Operation.OperationId;
    }
    /// <summary>Applies an already validated ordered lifecycle event batch.</summary>
    /// <param name="lifecycleEvents">The source events created from accepted business changes.</param>
    /// <param name="command">The originating command.</param>
    /// <returns>True when every source event is applied, including an idempotent empty batch.</returns>
    internal bool Update(IReadOnlyList<DatabaseBackupEventContract> lifecycleEvents, ICommand command)
    {
        if (lifecycleEvents.Any(sourceEvent => sourceEvent.CommandId != command.CommandId)) return false;
        foreach (var sourceEvent in lifecycleEvents)
            if (!Update(sourceEvent, command)) return false;
        return true;
    }

    internal DatabaseBackupTransition Compute(IDatabaseBackupCommand command)
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

    internal DatabaseBackupTransition Compute(DatabaseBackupInternalCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        EnsureExpectedRevision(command.ExpectedStateRevision);
        var backupChange = Translate(command);
        var fingerprint = Fingerprint(backupChange);
        if (_processedServiceEvents.TryGetValue(command.Source.SourceEventId, out var existing))
        {
            if (!StringComparer.Ordinal.Equals(existing, fingerprint))
                throw new InvalidOperationException("A service event ID was replayed with conflicting content.");
            return new([]);
        }
        EnsureServiceIdentityAndSequence(command.Source);
        EnsureLegalTransition(backupChange.DatabaseSource.Phase, backupChange.Outcome);
        return new([backupChange]);
    }

    static DatabaseBackupChange Translate(DatabaseBackupInternalCommand command)
        => command switch
        {
            RecordDatabaseOperationAdmissionCommand => CreateChange<DatabaseOperationAdmissionRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseOperationStartedCommand => CreateChange<DatabaseOperationStartedEvent>(command, DatabaseRecoveryPhase.Started),
            RecordDatabaseOperationProgressCommand => CreateChange<DatabaseOperationProgressRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseBackupBoundaryCommand => CreateChange<DatabaseBackupBoundaryRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseArtifactReplicaCommand => CreateChange<DatabaseArtifactReplicaRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseOperationVerificationCommand => command.Source.OperationKind is DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill
                ? CreateChange<DatabaseRestoreValidationRecordedEvent>(command, DatabaseRecoveryPhase.Validating)
                : CreateChange<DatabaseOperationVerificationRecordedEvent>(command, DatabaseRecoveryPhase.Verifying),
            RecordDatabaseOperationErrorCommand => CreateChange<DatabaseOperationErrorRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseRestoreReadyForCutoverCommand => CreateChange<DatabaseRestoreReadyForCutoverRecordedEvent>(command, DatabaseRecoveryPhase.ReadyForCutover),
            CompleteDatabaseOperationCommand => CreateChange<DatabaseOperationCompletedEvent>(command, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded),
            FailDatabaseOperationCommand => CreateChange<DatabaseOperationFailedEvent>(command, DatabaseRecoveryPhase.Failed, DatabaseRecoveryOutcome.Failed),
            RecordDatabaseOperationCancelledCommand => CreateChange<DatabaseOperationCancelledEvent>(command, DatabaseRecoveryPhase.Cancelled, DatabaseRecoveryOutcome.Cancelled),
            RecordDatabaseBackupPolicyStatusCommand => CreateChange<DatabaseBackupPolicyEnforcedEvent>(command, command.Source.Phase),
            RecordDatabaseRetentionResultCommand => CreateChange<DatabaseRetentionExecutionRequestedDomainEvent>(command, command.Source.Phase, command.Outcome),
            ReconcileDatabaseBackupServiceStateCommand => CreateChange<DatabaseBackupServiceReconciledEvent>(command, command.Source.Phase),
            RecordDatabaseBackupServiceCapabilityCommand => CreateChange<DatabaseBackupServiceCapabilityRecordedEvent>(command, command.Source.Phase),
            RecordDatabaseRecoveryRunStatisticsCommand => CreateChange<DatabaseRecoveryStatisticsRecordedEvent>(command, command.Source.Phase),
            _ => throw new InvalidOperationException($"Unsupported internal DatabaseBackup command '{command.GetType().Name}'.")
        };

    DatabaseBackupTransition RequestBackup(RequestDatabaseBackupCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        EnsureNewOperation(command.EntityId);
        var source = Source(command, DatabaseRecoveryOperationKind.Backup, DatabaseRecoveryPhase.Requested);
        lifecycleChanges.Add(CreateChange<DatabaseBackupRequestedDomainEvent>(command, source));
        lifecycleChanges.Add(CreateChange<DatabaseBackupAuthorizedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Authorized)));
        lifecycleChanges.Add(CreateChange<DatabaseBackupExecutionRequestedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Requested)));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition RequestRestore(RequestDatabaseRestoreCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        EnsureNewOperation(command.EntityId);
        var source = Source(command, DatabaseRecoveryOperationKind.Restore, DatabaseRecoveryPhase.Requested);
        lifecycleChanges.Add(CreateChange<DatabaseRestoreRequestedDomainEvent>(command, source));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition ApproveRestore(ApproveDatabaseRestoreCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        EnsureOperation(DatabaseRecoveryOperationKind.Restore, DatabaseRecoveryPhase.Requested);
        var source = Source(command, Operation.Kind, DatabaseRecoveryPhase.Authorized);
        lifecycleChanges.Add(CreateChange<DatabaseRestoreAuthorizedDomainEvent>(command, source));
        lifecycleChanges.Add(CreateChange<DatabaseRestoreExecutionRequestedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Requested)));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition RequestDrill(RequestDatabaseRestoreDrillCommand command)
    {
        // Existing callers supply the profile and logical validation target in these fields.
        // Materialize the executable descriptor before creating any source events.
        command = command with
        {
            FreshTarget = command.FreshTarget ?? new DatabaseFreshTargetDescriptor(command.DisposableTargetProfile, command.ValidationProfile),
            RestoreClass = DatabaseRestoreClass.Drill
        };
        List<DatabaseBackupChange> lifecycleChanges = [];
        EnsureNewOperation(command.EntityId);
        var source = Source(command, DatabaseRecoveryOperationKind.RestoreDrill, DatabaseRecoveryPhase.Requested);
        lifecycleChanges.Add(CreateChange<DatabaseRestoreDrillRequestedDomainEvent>(command, source));
        lifecycleChanges.Add(CreateChange<DatabaseRestoreDrillAuthorizedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Authorized)));
        lifecycleChanges.Add(CreateChange<DatabaseRestoreDrillExecutionRequestedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Requested)));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition ApproveCutover(ApproveDatabaseCutoverCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        EnsureOperation(DatabaseRecoveryOperationKind.Restore, DatabaseRecoveryPhase.ReadyForCutover);
        if (command.ValidationRevision != Operation.ValidationRevision)
            throw new InvalidOperationException("Cutover approval does not match the current validation revision.");
        var source = Source(command, DatabaseRecoveryOperationKind.Cutover, DatabaseRecoveryPhase.Authorized);
        lifecycleChanges.Add(CreateChange<DatabaseCutoverRequestedDomainEvent>(command, source));
        lifecycleChanges.Add(CreateChange<DatabaseCutoverAuthorizedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Authorized)));
        lifecycleChanges.Add(CreateChange<DatabaseCutoverExecutionRequestedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.CuttingOver)));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition Cancel(IDatabaseBackupCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        if (!Operation.Exists || Operation.IsTerminal) throw new InvalidOperationException("Only an active operation can be cancelled.");
        var source = Source(command, Operation.Kind, DatabaseRecoveryPhase.Cancelled);
        lifecycleChanges.Add(CreateChange<DatabaseOperationCancelledEvent>(command, source, DatabaseRecoveryOutcome.Cancelled));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition UpdatePolicy(UpdateDatabaseBackupPolicyCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        var source = Source(command, DatabaseRecoveryOperationKind.Reconciliation, DatabaseRecoveryPhase.Authorized);
        lifecycleChanges.Add(CreateChange<DatabaseBackupPolicyRevisedEvent>(command, source));
        lifecycleChanges.Add(CreateChange<DatabaseBackupPolicyEnforcedEvent>(command, NextSource(source, DatabaseRecoveryPhase.Authorized)));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition PlaceLegalHold(PlaceBackupLegalHoldCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        var source = Source(command, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Authorized);
        lifecycleChanges.Add(CreateChange<DatabaseBackupLegalHoldPlacedEvent>(command, source));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition ReleaseLegalHold(ReleaseBackupLegalHoldCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        var source = Source(command, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Authorized);
        lifecycleChanges.Add(CreateChange<DatabaseBackupLegalHoldReleasedEvent>(command, source));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition RequestRetention(RequestBackupRetentionEvaluationCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        EnsureNewOperation(command.EntityId);
        var source = Source(command, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Requested);
        lifecycleChanges.Add(CreateChange<DatabaseRetentionRequestedDomainEvent>(command, source));
        lifecycleChanges.Add(CreateChange<DatabaseRetentionAuthorizedDomainEvent>(command, NextSource(source, DatabaseRecoveryPhase.Authorized)));
        return new(lifecycleChanges.ToArray());
    }

    DatabaseBackupTransition ExecuteRetention(ExecuteBackupRetentionPlanCommand command)
    {
        List<DatabaseBackupChange> lifecycleChanges = [];
        var source = Source(command, DatabaseRecoveryOperationKind.Retention, DatabaseRecoveryPhase.Requested);
        lifecycleChanges.Add(CreateChange<DatabaseRetentionExecutionRequestedDomainEvent>(command, source));
        return new(lifecycleChanges.ToArray());
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
            ProgressPercent = Math.Max(Operation.ProgressPercent, e.ProgressPercent),
            HostId = source.ProducingHostId ?? Operation.HostId,
            LastServiceSequence = source.ProducingHostId is null ? Operation.LastServiceSequence : source.SourceRevisionOrSequence,
            ValidationRevision = e.ValidationRevision == 0 ? Operation.ValidationRevision : e.ValidationRevision,
            CutoverState = e.CutoverState == DatabaseCutoverState.None ? Operation.CutoverState : e.CutoverState,
            BackupLineage = e.BackupLineage ?? Operation.BackupLineage
        };
        if (source.ProducingHostId is not null)
        {
            _processedServiceEvents[source.SourceEventId] = Fingerprint(DatabaseBackupEventFactory.Describe(e));
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

    DatabaseSourceEnvelope Source(IDatabaseBackupCommand command, DatabaseRecoveryOperationKind kind, DatabaseRecoveryPhase phase)
        => new()
        {
            SourceEventId = Guid.NewGuid(),
            OperationId = command.EntityId,
            BackupSetId = command.BackupSetId,
            Source = command.Source == BackupSource.None ? Operation.Source : command.Source,
            ProtectionSetId = command.ProtectionSetId ?? (Operation.Exists ? Operation.ProtectionSetId : new DatabaseProtectionSetId("all")),
            PolicyRevision = command.ExpectedPolicyRevision == 0 ? Operation.PolicyRevision : command.ExpectedPolicyRevision,
            OperationKind = kind,
            Phase = phase,
            SourceRevisionOrSequence = 0,
            CorrelationId = command.Request.CorrelationId,
            CausationId = command.Request.CausationId,
            ObservedUtc = command.Request.CreatedUtc
        };

    static DatabaseSourceEnvelope NextSource(DatabaseSourceEnvelope source, DatabaseRecoveryPhase phase)
        => source with { SourceEventId = Guid.NewGuid(), Phase = phase };

    static DatabaseBackupChange CreateChange<TEvent>(IDatabaseBackupCommand command, DatabaseSourceEnvelope source, DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None)
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
            BackupLineage = command is RequestDatabaseBackupCommand
                ? new DatabaseBackupLineage
                {
                    RequestedMode = command.RequestedBackupMode == DatabaseBackupMode.None
                        ? DatabaseBackupMode.Full
                        : command.RequestedBackupMode
                }
                : null
        };
    }

    static DatabaseBackupChange CreateChange<TEvent>(DatabaseBackupInternalCommand command, DatabaseRecoveryPhase phase, DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None)
        where TEvent : DatabaseBackupEventContract, new()
    {
        var source = command.Source with { Phase = phase };
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

    internal static string Fingerprint(DatabaseBackupChange backupChange)
        => JsonSerializer.Serialize(new
        {
            Type = backupChange.EventFamily.FullName,
            Source = backupChange.DatabaseSource,
            backupChange.ProgressPercent,
            backupChange.SafeDiagnosticReference,
            backupChange.ArtifactReplica,
            backupChange.VerificationLevel,
            backupChange.Outcome,
            backupChange.ErrorClassification,
            backupChange.CutoverState,
            backupChange.CapabilityState,
            backupChange.Statistics,
            backupChange.RestorePointId,
            backupChange.FreshTarget,
            Policy = backupChange.BackupPolicy,
            backupChange.RequiredDestinations,
            backupChange.ValidationRevision,
            backupChange.RetentionPlanId,
            backupChange.RetentionPlanRevision,
            backupChange.RestoreClass,
            backupChange.EvaluationBoundaryUtc,
            backupChange.PolicyId,
            backupChange.ManifestRevision,
            backupChange.BackupLineage
        });
}
