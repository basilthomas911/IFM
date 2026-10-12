using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

public sealed class DatabaseBackupActorStateTests
{
    [Fact]
    public void Automatic_retention_command_retry_does_not_publish_another_execution_intent()
    {
        var state = new DatabaseBackupCommandState();
        var command = new RequestBackupRetentionEvaluationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = new(Guid.NewGuid()),
            Request = Request() with { Origin = DatabaseRequestOrigin.ScheduledTask },
            Source = BackupSource.AwsCloud, ProtectionSetId = new("core"),
            RetentionPlanId = new(Guid.NewGuid()), RetentionPlanRevision = 1,
            EvaluationBoundaryUtc = DateTimeOffset.UtcNow.AddDays(-35)
        };
        state.Execute(command);
        var revision = state.Operation.Revision;
        state.Events.Clear();
        state.Execute(command);
        state.Events.Should().BeEmpty();
        state.Operation.Revision.Should().Be(revision);
        var different = command with { RetentionPlanRevision = 2 };
        Action retry = () => state.Execute(different);
        retry.Should().Throw<InvalidOperationException>();
    }
    [Fact]
    public void Recording_a_retention_result_does_not_request_another_execution()
    {
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(new RequestBackupRetentionEvaluationCommand
        {
            CommandId = Guid.NewGuid(), EntityId = operation, Request = Request(),
            Source = BackupSource.LocalWorkstation, ProtectionSetId = new DatabaseProtectionSetId("core"),
            RetentionPlanId = new DatabaseRetentionPlanId(Guid.NewGuid()), RetentionPlanRevision = 1,
            EvaluationBoundaryUtc = DateTimeOffset.UtcNow.AddDays(-35)
        });
        state.Execute(Internal<RecordDatabaseOperationAdmissionCommand>(operation, 1, DatabaseRecoveryPhase.Admitted, kind: DatabaseRecoveryOperationKind.Retention));
        state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operation, 2, DatabaseRecoveryPhase.Started, kind: DatabaseRecoveryOperationKind.Retention));
        state.Events.Clear();
        state.Execute(Internal<RecordDatabaseRetentionResultCommand>(operation, 3, DatabaseRecoveryPhase.Completed,
            kind: DatabaseRecoveryOperationKind.Retention) with { Outcome = DatabaseRecoveryOutcome.Succeeded });
        var result = state.Events.Single().Should().BeOfType<DatabaseRetentionResultRecordedDomainEvent>().Subject;
        DatabaseBackupStateRepository.ToExecutionEvent(result).Should().BeNull();
    }
    [Fact]
    public async Task State_repository_publishes_using_the_concrete_event_contract()
    {
        var context = Substitute.For<ICommandActorContext>();
        var domainEvent = new DatabaseBackupRequestedDomainEvent();

        await DatabaseBackupStateRepository.SendConcreteEventAsync(context, domainEvent);

        await context.Received(1)
            .SendAsync<DatabaseBackupRequestedDomainEvent, DatabaseRecoveryOperationId>(domainEvent);
    }

    [Fact]
    public void Every_phase_3_contract_has_exactly_one_actor_route()
    {
        var assembly = typeof(DatabaseBackupCommandRoute).Assembly;
        var commandTypes = assembly.GetTypes().Where(type => !type.IsAbstract
            && (typeof(IDatabaseBackupCommand).IsAssignableFrom(type) || typeof(DatabaseBackupInternalCommand).IsAssignableFrom(type))).ToHashSet();
        var serviceEventTypes = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(DatabaseBackupServiceEventContract).IsAssignableFrom(type)).ToHashSet();
        var queryTypes = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IDatabaseBackupQuery).IsAssignableFrom(type)).ToHashSet();

        DatabaseBackupCommandActor.SupportedCommandTypes.Should().BeEquivalentTo(commandTypes);
        DatabaseBackupCommandActor.SupportedVerbs.Should().OnlyHaveUniqueItems().And.HaveCount(commandTypes.Count);
        DatabaseBackupEventActor.SupportedServiceEventTypes.Should().BeEquivalentTo(serviceEventTypes);
        DatabaseBackupEventActor.SupportedVerbs.Should().OnlyHaveUniqueItems().And.HaveCount(serviceEventTypes.Count);
        DatabaseBackupQueryActor.SupportedQueryTypes.Should().BeEquivalentTo(queryTypes);
        DatabaseBackupQueryActor.SupportedVerbs.Should().OnlyHaveUniqueItems().And.HaveCount(queryTypes.Count);
    }

    [Fact]
    public void Every_service_event_translates_to_one_internal_command_shape()
    {
        var source = ServiceSource(Guid.NewGuid(), 1, DatabaseRecoveryPhase.Admitted);
        foreach (var type in DatabaseBackupEventActor.SupportedServiceEventTypes)
        {
            var template = (DatabaseBackupServiceEventContract)Activator.CreateInstance(type)!;
            var serviceEvent = (DatabaseBackupServiceEventContract)(template with
            {
                Id = source.SourceEventId,
                EntityId = source.OperationId,
                CommandId = source.CorrelationId,
                Subject = new ActorSubject(ActorType.Event, DatabaseBackupEventActor.Actor, template.Verb, source.OperationId.Format()),
                Source = source,
                ReceivedOn = source.ObservedUtc.UtcDateTime,
                Outcome = type.Name.Contains("Failed", StringComparison.Ordinal) ? DatabaseRecoveryOutcome.Failed : DatabaseRecoveryOutcome.None
            });

            var command = DatabaseBackupEventTranslator.Translate(serviceEvent);

            command.Source.Should().Be(source);
            command.EntityId.Should().Be(source.OperationId);
            command.Subject.Name.Should().Be(DatabaseBackupCommandRoute.Actor);
        }
    }

    [Fact]
    public void Backup_lifecycle_accepts_ordered_service_observations_and_rejects_terminal_progress()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId));
        state.Operation.Phase.Should().Be(DatabaseRecoveryPhase.Requested);
        state.Events.Should().HaveCount(3);
        state.Events.OfType<DatabaseBackupEventContract>()
            .Select(item => item.Source.SourceEventId)
            .Should().OnlyHaveUniqueItems();

        state.Execute(Internal<RecordDatabaseOperationAdmissionCommand>(operationId, 1, DatabaseRecoveryPhase.Admitted));
        state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operationId, 2, DatabaseRecoveryPhase.Started));
        state.Execute(Internal<RecordDatabaseOperationProgressCommand>(operationId, 3, DatabaseRecoveryPhase.Capturing, progress: 40));
        state.Execute(Internal<CompleteDatabaseOperationCommand>(operationId, 4, DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded));

        state.Operation.Phase.Should().Be(DatabaseRecoveryPhase.Completed);
        state.Operation.Outcome.Should().Be(DatabaseRecoveryOutcome.Succeeded);
        state.Operation.LastServiceSequence.Should().Be(4);
        var progressAfterTerminal = () => state.Execute(Internal<RecordDatabaseOperationProgressCommand>(operationId, 5, DatabaseRecoveryPhase.Transferring));
        progressAfterTerminal.Should().Throw<InvalidOperationException>().WithMessage("*Terminal*");
    }

    [Fact]
    public void Backup_state_preserves_requested_mode_and_adopts_resolved_native_lineage()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId) with { RequestedBackupMode = DatabaseBackupMode.Automatic });

        state.Operation.BackupLineage.Should().BeEquivalentTo(new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Automatic
        });

        var resolved = new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Automatic,
            ResolvedMode = DatabaseBackupMode.Incremental,
            NativeKind = DatabaseNativeBackupKind.PostgreSqlIncremental,
            BaseRestorePointId = new DatabaseRestorePointId("base-001"),
            ParentRestorePointId = new DatabaseRestorePointId("incremental-002"),
            ChainDepth = 3,
            NativeIdentity = "postgres-system-42"
        };
        state.Execute(Internal<RecordDatabaseOperationAdmissionCommand>(
            operationId, 1, DatabaseRecoveryPhase.Admitted, backupLineage: resolved));

        state.Operation.BackupLineage.Should().BeEquivalentTo(resolved);
    }

    [Fact]
    public void Duplicate_service_event_is_idempotent_but_conflicting_content_is_rejected()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId));
        var eventId = Guid.NewGuid();
        var admission = Internal<RecordDatabaseOperationAdmissionCommand>(operationId, 1, DatabaseRecoveryPhase.Admitted, eventId: eventId);
        state.Execute(admission);
        var revision = state.Operation.Revision;
        var eventCount = state.Events.Count;

        state.Execute(admission);

        state.Operation.Revision.Should().Be(revision);
        state.Events.Should().HaveCount(eventCount);
        var conflict = Internal<RecordDatabaseOperationAdmissionCommand>(operationId, 1, DatabaseRecoveryPhase.Started, eventId: eventId);
        ((Action)(() => state.Execute(conflict))).Should().Throw<InvalidOperationException>().WithMessage("*conflicting*");
    }

    [Fact]
    public void Duplicate_service_event_remains_idempotent_after_event_replay()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId));
        var requestEvents = state.Events.ToArray();
        state.Events.Clear();
        var eventId = Guid.NewGuid();
        var admission = Internal<RecordDatabaseOperationAdmissionCommand>(
            operationId,
            1,
            DatabaseRecoveryPhase.Admitted,
            eventId: eventId);
        state.Execute(admission);
        var admissionEvent = state.Events.Single();

        var reloaded = new DatabaseBackupCommandState();
        reloaded.ReplayEvents([.. requestEvents, admissionEvent]);
        var revision = reloaded.Operation.Revision;

        reloaded.Execute(admission);

        reloaded.Operation.Revision.Should().Be(revision);
        reloaded.Events.Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_service_event_remains_idempotent_after_persisted_json_replay()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId));
        var history = state.Events.ToList();
        state.Events.Clear();
        var eventId = Guid.NewGuid();
        var admission = Internal<RecordDatabaseOperationAdmissionCommand>(
            operationId,
            1,
            DatabaseRecoveryPhase.Admitted,
            eventId: eventId);
        state.Execute(admission);
        history.Add(state.Events.Single());

        var persisted = history.Select((domainEvent, index) => new EventStreamReadModel
        {
            EventVersion = index + 1,
            EventTypeName = domainEvent.GetType().AssemblyQualifiedName!,
            EventData = TomasAI.IFM.Shared.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(domainEvent)
        }).ToArray();
        var reloaded = new DatabaseBackupCommandState();
        reloaded.ReplayEvents(persisted);
        var revision = reloaded.Operation.Revision;

        reloaded.Execute(admission);

        reloaded.Operation.Revision.Should().Be(revision);
        reloaded.Events.Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_service_event_with_changed_payload_is_rejected()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId));
        state.Execute(Internal<RecordDatabaseOperationAdmissionCommand>(operationId, 1, DatabaseRecoveryPhase.Admitted));
        state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operationId, 2, DatabaseRecoveryPhase.Started));
        var eventId = Guid.NewGuid();
        state.Execute(Internal<RecordDatabaseOperationProgressCommand>(operationId, 3, DatabaseRecoveryPhase.Capturing, progress: 25, eventId: eventId));

        var conflict = Internal<RecordDatabaseOperationProgressCommand>(operationId, 3, DatabaseRecoveryPhase.Capturing, progress: 75, eventId: eventId);

        ((Action)(() => state.Execute(conflict))).Should().Throw<InvalidOperationException>().WithMessage("*conflicting*");
    }

    [Fact]
    public void Sequence_gaps_source_changes_and_host_changes_are_rejected()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId));
        state.Execute(Internal<RecordDatabaseOperationAdmissionCommand>(operationId, 1, DatabaseRecoveryPhase.Admitted));

        ((Action)(() => state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operationId, 3, DatabaseRecoveryPhase.Started))))
            .Should().Throw<InvalidOperationException>().WithMessage("*sequence gap*");
        ((Action)(() => state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operationId, 2, DatabaseRecoveryPhase.Started, host: "host-2"))))
            .Should().Throw<InvalidOperationException>().WithMessage("*host changed*");
        ((Action)(() => state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operationId, 2, DatabaseRecoveryPhase.Started, source: BackupSource.AwsCloud))))
            .Should().Throw<InvalidOperationException>().WithMessage("*immutable operation definition*");
    }

    [Fact]
    public void Restore_requires_approval_validation_and_revision_bound_cutover()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestRestore(operationId));
        state.Operation.Phase.Should().Be(DatabaseRecoveryPhase.Requested);
        state.Operation.BackupLineage.Should().BeNull();

        state.Execute(new ApproveDatabaseRestoreCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = operationId,
            Request = Request(),
            ApprovalIdentity = "approver-1",
            ApprovalReference = "approval-restore",
            ExpectedStateRevision = state.Operation.Revision
        });
        state.Execute(Internal<RecordDatabaseOperationAdmissionCommand>(operationId, 1, DatabaseRecoveryPhase.Admitted, kind: DatabaseRecoveryOperationKind.Restore));
        state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operationId, 2, DatabaseRecoveryPhase.Started, kind: DatabaseRecoveryOperationKind.Restore));
        state.Execute(Internal<RecordDatabaseOperationVerificationCommand>(operationId, 3, DatabaseRecoveryPhase.Validating, validationRevision: 12, kind: DatabaseRecoveryOperationKind.Restore));
        state.Execute(Internal<RecordDatabaseRestoreReadyForCutoverCommand>(operationId, 4, DatabaseRecoveryPhase.ReadyForCutover, validationRevision: 12, kind: DatabaseRecoveryOperationKind.Restore));

        var stale = new ApproveDatabaseCutoverCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = operationId,
            Request = Request(),
            ApprovalIdentity = "approver-2",
            ApprovalReference = "approval-cutover",
            ValidationRevision = 11,
            ExpectedStateRevision = state.Operation.Revision
        };
        ((Action)(() => state.Execute(stale))).Should().Throw<InvalidOperationException>().WithMessage("*validation revision*");

        state.Execute(stale with { ValidationRevision = 12 });
        state.Operation.Phase.Should().Be(DatabaseRecoveryPhase.CuttingOver);
        state.Restore.FreshTarget.Should().Be(RequestRestore(operationId).FreshTarget);
        state.Restore.RestorePointId.Should().Be(RequestRestore(operationId).RestorePointId);
        state.Execute(Internal<FailDatabaseOperationCommand>(operationId, 5, DatabaseRecoveryPhase.Failed,
            validationRevision: 12, kind: DatabaseRecoveryOperationKind.Cutover));
        ((Action)(() => state.Execute(stale with { ExpectedStateRevision = state.Operation.Revision })))
            .Should().Throw<InvalidOperationException>().WithMessage("*validation revision*");
        state.Execute(stale with { CommandId = Guid.NewGuid(), ValidationRevision = 12, ExpectedStateRevision = state.Operation.Revision });
        state.Operation.Phase.Should().Be(DatabaseRecoveryPhase.CuttingOver);
        state.Restore.RestorePointId.Should().Be(RequestRestore(operationId).RestorePointId);
    }

    [Fact]
    public void Restored_missing_cutover_state_can_reconcile_only_validated_external_journal_identity()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        var reconcile = Internal<ReconcileDatabaseBackupServiceStateCommand>(operationId, 4,
            DatabaseRecoveryPhase.CuttingOver, validationRevision: 12, kind: DatabaseRecoveryOperationKind.Cutover) with
        {
            RestorePointId = new DatabaseRestorePointId("verified-current-point"),
            FreshTarget = new DatabaseFreshTargetDescriptor("disposable-validation", "gate7"),
            CutoverState = DatabaseCutoverState.InProgress,
            VerificationLevel = DatabaseVerificationLevel.ApplicationValidation
        };
        ((Action)(() => state.Execute(reconcile with { ValidationRevision = 0 })))
            .Should().Throw<InvalidOperationException>().WithMessage("*existing operation*");
        state.Execute(reconcile);
        state.Operation.Kind.Should().Be(DatabaseRecoveryOperationKind.Cutover);
        state.Operation.LastServiceSequence.Should().Be(4);
        state.Restore.FreshTarget.Should().Be(reconcile.FreshTarget);
        var revision = state.Operation.Revision;
        state.Execute(reconcile with { CommandId = Guid.NewGuid(), Source = reconcile.Source with { SourceEventId = Guid.NewGuid() } });
        state.Operation.Revision.Should().Be(revision);
        state.Execute(Internal<CompleteDatabaseOperationCommand>(operationId, 5, DatabaseRecoveryPhase.Completed,
            outcome: DatabaseRecoveryOutcome.Succeeded, kind: DatabaseRecoveryOperationKind.Cutover));
        state.Operation.Phase.Should().Be(DatabaseRecoveryPhase.Completed);
    }

    [Theory]
    [InlineData(DatabaseRecoveryPhase.Completed, DatabaseRecoveryOutcome.Succeeded)]
    [InlineData(DatabaseRecoveryPhase.Failed, DatabaseRecoveryOutcome.Failed)]
    public void Missing_terminal_retention_display_can_recover_without_execution(DatabaseRecoveryPhase phase, DatabaseRecoveryOutcome outcome)
    {
        var id = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        var command = Internal<ReconcileDatabaseBackupServiceStateCommand>(id, 4, phase, outcome: outcome,
            source: BackupSource.AwsCloud, kind: DatabaseRecoveryOperationKind.Retention) with
        { RetentionPlanId = new DatabaseRetentionPlanId(Guid.NewGuid()), RetentionPlanRevision = 1 };
        ((Action)(() => state.Execute(command with { RetentionPlanRevision = 0 })))
            .Should().Throw<InvalidOperationException>().WithMessage("*existing operation*");
        ((Action)(() => state.Execute(command with { Outcome = DatabaseRecoveryOutcome.None })))
            .Should().Throw<InvalidOperationException>().WithMessage("*existing operation*");
        state.Execute(command);
        state.Operation.Kind.Should().Be(DatabaseRecoveryOperationKind.Retention);
        state.Operation.Phase.Should().Be(phase);
        state.Operation.LastServiceSequence.Should().Be(4);
        var revision = state.Operation.Revision;
        state.Execute(command);
        state.Operation.Revision.Should().Be(revision);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("policy")]
    [InlineData("plan")]
    [InlineData("sequence")]
    [InlineData("outcome")]
    public void Terminal_retention_display_repair_cannot_change_independent_identity(string mismatch)
    {
        var id = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        var command = Internal<ReconcileDatabaseBackupServiceStateCommand>(id, 4, DatabaseRecoveryPhase.Completed,
            outcome: DatabaseRecoveryOutcome.Succeeded, source: BackupSource.AwsCloud,
            kind: DatabaseRecoveryOperationKind.Retention) with
        { RetentionPlanId = new DatabaseRetentionPlanId(Guid.NewGuid()), RetentionPlanRevision = 1 };
        state.Execute(command);
        var nextId = Guid.NewGuid();
        var repair = command with { CommandId = nextId, Source = command.Source with { SourceEventId = nextId } };
        repair = mismatch switch
        {
            "host" => repair with { Source = repair.Source with { ProducingHostId = new DatabaseBackupHostId("other-host") } },
            "policy" => repair with { Source = repair.Source with { PolicyRevision = 3 } },
            "plan" => repair with { RetentionPlanId = new DatabaseRetentionPlanId(Guid.NewGuid()) },
            "sequence" => repair with { Source = repair.Source with { SourceRevisionOrSequence = 5 } },
            _ => repair with { Source = repair.Source with { Phase = DatabaseRecoveryPhase.Failed }, Outcome = DatabaseRecoveryOutcome.Failed }
        };
        ((Action)(() => state.Execute(repair))).Should().Throw<InvalidOperationException>();
        state.Operation.Phase.Should().Be(DatabaseRecoveryPhase.Completed);
        state.Operation.LastServiceSequence.Should().Be(4);
        state.Retention.PlanId.Should().Be(command.RetentionPlanId);
    }

    [Fact]
    public void Expected_state_revision_and_start_order_are_enforced()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId));

        var staleCancel = new CancelDatabaseBackupCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = operationId,
            Request = Request(),
            SafeReason = "operator cancel",
            ExpectedStateRevision = 1
        };
        ((Action)(() => state.Execute(staleCancel))).Should().Throw<InvalidOperationException>().WithMessage("*Expected revision*");
        ((Action)(() => state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operationId, 1, DatabaseRecoveryPhase.Started))))
            .Should().Throw<InvalidOperationException>().WithMessage("*requires admitted*");
    }

    [Fact]
    public async Task Execution_outbox_is_idempotent_and_tracks_until_publish_confirmation()
    {
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(new DatabaseRecoveryOperationId(Guid.NewGuid())));
        var domainIntent = state.Events.OfType<DatabaseBackupExecutionRequestedDomainEvent>().Single();
        var workOrder = DatabaseBackupStateRepository.ToExecutionEvent(domainIntent)!;
        var outbox = new DatabaseBackupExecutionOutbox();

        await outbox.EnqueueAsync(workOrder);
        await outbox.EnqueueAsync(workOrder);

        outbox.Pending.Should().ContainSingle().Which.Should().Be(workOrder);
        await outbox.MarkPublishedAsync(workOrder.Id);
        outbox.Pending.Should().BeEmpty();
    }

    [Fact]
    public void On_demand_restore_materializes_executable_target_in_all_source_events()
    {
        var state = new DatabaseBackupCommandState();
        var command = new RequestDatabaseRestoreDrillCommand
        {
            CommandId = Guid.NewGuid(), EntityId = new(Guid.NewGuid()), Request = Request(),
            Source = BackupSource.LocalWorkstation, ProtectionSetId = new("core-postgresql"),
            RestorePointId = new("verified-development-backup"),
            DisposableTargetProfile = "development-on-demand", ValidationProfile = "development-restore"
        };
        state.Execute(command);
        var events = state.Events.OfType<DatabaseBackupEventContract>().ToArray();
        events.Should().HaveCount(3).And.OnlyContain(item => item.CommandId == command.CommandId);
        events.Should().OnlyContain(item => item.FreshTarget != null && item.FreshTarget.Profile == "development-on-demand" && item.FreshTarget.LogicalTarget == "development-restore" && item.RestoreClass == DatabaseRestoreClass.Drill);
        var execution = DatabaseBackupStateRepository.ToExecutionEvent(events.Last());
        execution!.FreshTarget.Should().Be(new DatabaseFreshTargetDescriptor("development-on-demand", "development-restore"));
    }

    static RequestDatabaseBackupCommand RequestBackup(DatabaseRecoveryOperationId operationId) => new()
    {
        CommandId = Guid.NewGuid(),
        EntityId = operationId,
        Request = Request(),
        Source = BackupSource.LocalWorkstation,
        ProtectionSetId = new DatabaseProtectionSetId("core"),
        ConsistencyMode = DatabaseConsistencyMode.CoordinatedProtectionSet,
        RequiredDestinations = [new DatabaseLogicalDestination("vault", true)],
        ExpectedPolicyRevision = 2
    };

    static RequestDatabaseRestoreCommand RequestRestore(DatabaseRecoveryOperationId operationId) => new()
    {
        CommandId = Guid.NewGuid(),
        EntityId = operationId,
        Request = Request(),
        Source = BackupSource.LocalWorkstation,
        ProtectionSetId = new DatabaseProtectionSetId("core"),
        RestorePointId = new DatabaseRestorePointId("rp-001"),
        FreshTarget = new DatabaseFreshTargetDescriptor("isolated", "restore-target"),
        RestoreClass = DatabaseRestoreClass.ProductionRecovery,
        ExpectedPolicyRevision = 2,
        ExpectedManifestRevision = 1
    };

    static TCommand Internal<TCommand>(
        DatabaseRecoveryOperationId operationId, long sequence, DatabaseRecoveryPhase phase,
        DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None, int progress = 0, Guid? eventId = null,
        string host = "host-1", BackupSource source = BackupSource.LocalWorkstation, long validationRevision = 0,
        DatabaseRecoveryOperationKind kind = DatabaseRecoveryOperationKind.Backup,
        DatabaseBackupLineage? backupLineage = null)
        where TCommand : DatabaseBackupInternalCommand, new()
    {
        var envelope = ServiceSource(operationId.Value, sequence, phase, eventId, host, source, kind);
        var template = new TCommand();
        return (TCommand)(template with
        {
            CommandId = envelope.SourceEventId,
            EntityId = operationId,
            Source = envelope,
            Subject = new ActorSubject(ActorType.Command, DatabaseBackupCommandRoute.Actor, template.Verb, operationId.Format()),
            Outcome = outcome,
            ProgressPercent = progress,
            ValidationRevision = validationRevision,
            BackupLineage = backupLineage
        });
    }

    static DatabaseSourceEnvelope ServiceSource(Guid operationValue, long sequence, DatabaseRecoveryPhase phase,
        Guid? eventId = null, string host = "host-1", BackupSource source = BackupSource.LocalWorkstation,
        DatabaseRecoveryOperationKind kind = DatabaseRecoveryOperationKind.Backup) => new()
        {
            SourceEventId = eventId ?? Guid.NewGuid(),
            OperationId = new DatabaseRecoveryOperationId(operationValue),
            Source = source,
            ProtectionSetId = new DatabaseProtectionSetId("core"),
            PolicyRevision = 2,
            OperationKind = kind,
            Phase = phase,
            ProducingHostId = new DatabaseBackupHostId(host),
            SourceRevisionOrSequence = sequence,
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ObservedUtc = DateTimeOffset.UtcNow
        };

    static DatabaseRequestEnvelope Request() => new()
    {
        RequestId = Guid.NewGuid(),
        CallerIdentity = "operator",
        AuthorizationReference = "approval",
        CallerRoles = ["DatabaseRecoveryOperator"],
        Origin = DatabaseRequestOrigin.Console,
        CorrelationId = Guid.NewGuid(),
        EnvironmentIdentity = "paper-trading",
        CreatedUtc = DateTimeOffset.UtcNow
    };
}
