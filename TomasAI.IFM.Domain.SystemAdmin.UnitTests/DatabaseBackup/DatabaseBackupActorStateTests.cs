using FluentAssertions;
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

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

public sealed class DatabaseBackupActorStateTests
{
    [Fact]
    public void Every_phase_3_contract_has_exactly_one_actor_route()
    {
        var assembly = typeof(DatabaseBackupCommand).Assembly;
        var commandTypes = assembly.GetTypes().Where(type => !type.IsAbstract
            && (typeof(DatabaseBackupCommand).IsAssignableFrom(type) || typeof(DatabaseBackupInternalCommand).IsAssignableFrom(type))).ToHashSet();
        var serviceEventTypes = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(DatabaseBackupServiceEventContract).IsAssignableFrom(type)).ToHashSet();
        var queryTypes = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(DatabaseBackupQuery).IsAssignableFrom(type)).ToHashSet();

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
                Id = source.SourceEventId, EntityId = source.OperationId, CommandId = source.CorrelationId,
                Subject = new ActorSubject(ActorType.Event, DatabaseBackupEventActor.Actor, template.Verb, source.OperationId.Format()),
                Source = source, ReceivedOn = source.ObservedUtc.UtcDateTime,
                Outcome = type.Name.Contains("Failed", StringComparison.Ordinal) ? DatabaseRecoveryOutcome.Failed : DatabaseRecoveryOutcome.None
            });

            var command = DatabaseBackupEventTranslator.Translate(serviceEvent);

            command.Source.Should().Be(source);
            command.EntityId.Should().Be(source.OperationId);
            command.Subject.Name.Should().Be(DatabaseBackupCommand.Actor);
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

        state.Execute(new ApproveDatabaseRestoreCommand
        {
            CommandId = Guid.NewGuid(), EntityId = operationId, Request = Request(),
            ApprovalIdentity = "approver-1", ApprovalReference = "approval-restore", ExpectedStateRevision = state.Operation.Revision
        });
        state.Execute(Internal<RecordDatabaseOperationAdmissionCommand>(operationId, 1, DatabaseRecoveryPhase.Admitted, kind: DatabaseRecoveryOperationKind.Restore));
        state.Execute(Internal<RecordDatabaseOperationStartedCommand>(operationId, 2, DatabaseRecoveryPhase.Started, kind: DatabaseRecoveryOperationKind.Restore));
        state.Execute(Internal<RecordDatabaseOperationVerificationCommand>(operationId, 3, DatabaseRecoveryPhase.Validating, validationRevision: 12, kind: DatabaseRecoveryOperationKind.Restore));
        state.Execute(Internal<RecordDatabaseRestoreReadyForCutoverCommand>(operationId, 4, DatabaseRecoveryPhase.ReadyForCutover, validationRevision: 12, kind: DatabaseRecoveryOperationKind.Restore));

        var stale = new ApproveDatabaseCutoverCommand
        {
            CommandId = Guid.NewGuid(), EntityId = operationId, Request = Request(), ApprovalIdentity = "approver-2",
            ApprovalReference = "approval-cutover", ValidationRevision = 11, ExpectedStateRevision = state.Operation.Revision
        };
        ((Action)(() => state.Execute(stale))).Should().Throw<InvalidOperationException>().WithMessage("*validation revision*");

        state.Execute(stale with { ValidationRevision = 12 });
        state.Operation.Phase.Should().Be(DatabaseRecoveryPhase.CuttingOver);
    }

    [Fact]
    public void Expected_state_revision_and_start_order_are_enforced()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        state.Execute(RequestBackup(operationId));

        var staleCancel = new CancelDatabaseBackupCommand
        {
            CommandId = Guid.NewGuid(), EntityId = operationId, Request = Request(), SafeReason = "operator cancel", ExpectedStateRevision = 1
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

    static RequestDatabaseBackupCommand RequestBackup(DatabaseRecoveryOperationId operationId) => new()
    {
        CommandId = Guid.NewGuid(), EntityId = operationId, Request = Request(), Source = BackupSource.LocalWorkstation,
        ProtectionSetId = new DatabaseProtectionSetId("core"), ConsistencyMode = DatabaseConsistencyMode.CoordinatedProtectionSet,
        RequiredDestinations = [new DatabaseLogicalDestination("vault", true)], ExpectedPolicyRevision = 2
    };

    static RequestDatabaseRestoreCommand RequestRestore(DatabaseRecoveryOperationId operationId) => new()
    {
        CommandId = Guid.NewGuid(), EntityId = operationId, Request = Request(), Source = BackupSource.LocalWorkstation,
        ProtectionSetId = new DatabaseProtectionSetId("core"), RestorePointId = new DatabaseRestorePointId("rp-001"),
        FreshTarget = new DatabaseFreshTargetDescriptor("isolated", "restore-target"), RestoreClass = DatabaseRestoreClass.ProductionRecovery,
        ExpectedPolicyRevision = 2, ExpectedManifestRevision = 1
    };

    static TCommand Internal<TCommand>(
        DatabaseRecoveryOperationId operationId, long sequence, DatabaseRecoveryPhase phase,
        DatabaseRecoveryOutcome outcome = DatabaseRecoveryOutcome.None, int progress = 0, Guid? eventId = null,
        string host = "host-1", BackupSource source = BackupSource.LocalWorkstation, long validationRevision = 0,
        DatabaseRecoveryOperationKind kind = DatabaseRecoveryOperationKind.Backup)
        where TCommand : DatabaseBackupInternalCommand, new()
    {
        var envelope = ServiceSource(operationId.Value, sequence, phase, eventId, host, source, kind);
        var template = new TCommand();
        return (TCommand)(template with
        {
            CommandId = envelope.SourceEventId, EntityId = operationId, Source = envelope,
            Subject = new ActorSubject(ActorType.Command, DatabaseBackupCommand.Actor, template.Verb, operationId.Format()),
            Outcome = outcome, ProgressPercent = progress, ValidationRevision = validationRevision
        });
    }

    static DatabaseSourceEnvelope ServiceSource(Guid operationValue, long sequence, DatabaseRecoveryPhase phase,
        Guid? eventId = null, string host = "host-1", BackupSource source = BackupSource.LocalWorkstation,
        DatabaseRecoveryOperationKind kind = DatabaseRecoveryOperationKind.Backup) => new()
    {
        SourceEventId = eventId ?? Guid.NewGuid(), OperationId = new DatabaseRecoveryOperationId(operationValue),
        Source = source, ProtectionSetId = new DatabaseProtectionSetId("core"), PolicyRevision = 2,
        OperationKind = kind, Phase = phase, ProducingHostId = new DatabaseBackupHostId(host),
        SourceRevisionOrSequence = sequence, CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow
    };

    static DatabaseRequestEnvelope Request() => new()
    {
        RequestId = Guid.NewGuid(), CallerIdentity = "operator", AuthorizationReference = "approval",
        CallerRoles = ["DatabaseRecoveryOperator"], Origin = DatabaseRequestOrigin.Console,
        CorrelationId = Guid.NewGuid(), EnvironmentIdentity = "paper-trading", CreatedUtc = DateTimeOffset.UtcNow
    };
}
