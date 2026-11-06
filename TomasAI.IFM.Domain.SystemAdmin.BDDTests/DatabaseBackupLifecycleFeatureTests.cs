using FluentAssertions;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests;

public sealed class DatabaseBackupLifecycleFeatureTests
{
    [Fact]
    public void Given_an_authorized_backup_request_when_core_accepts_it_then_durable_execution_intent_is_recorded()
    {
        var command = BackupRequest();
        var state = new DatabaseBackupCommandState();

        var result = command.Execute(state);

        Assert.True(result.Success, result.ErrorMessage);
        state.Events.Should().ContainInOrder(
            state.Events.OfType<DatabaseBackupRequestedDomainEvent>().Single(),
            state.Events.OfType<DatabaseBackupAuthorizedDomainEvent>().Single(),
            state.Events.OfType<DatabaseBackupExecutionRequestedDomainEvent>().Single());
        state.Operation.Source.Should().Be(BackupSource.LocalWorkstation);
        state.Operation.PolicyRevision.Should().Be(1);
    }

    [Fact]
    public void Given_a_host_admitted_backup_when_it_reloads_then_duplicate_admission_does_not_prevent_progress()
    {
        var command = BackupRequest();
        var state = new DatabaseBackupCommandState();
        Assert.True(command.Execute(state).Success);
        var admission = Observation<RecordDatabaseOperationAdmissionCommand>(1, DatabaseRecoveryPhase.Admitted);
        var admitted = admission.Execute(state);
        Assert.True(admitted.Success, admitted.ErrorMessage);
        var restored = new DatabaseBackupCommandState();
        restored.ReplayEvents(state.Events.ToArray());
        Assert.Equal(state.Operation, restored.Operation);

        var duplicate = admission.Execute(restored);
        Assert.True(duplicate.Success, duplicate.ErrorMessage);
        Assert.Empty(restored.Events);
        var started = Observation<RecordDatabaseOperationStartedCommand>(2, DatabaseRecoveryPhase.Started).Execute(restored);
        Assert.True(started.Success, started.ErrorMessage);
        Assert.IsType<DatabaseOperationStartedEvent>(Assert.Single(restored.Events));
        Assert.Equal(DatabaseRecoveryPhase.Started, restored.Operation.Phase);
        Assert.Equal(2, restored.Operation.LastServiceSequence);

        T Observation<T>(long sequence, DatabaseRecoveryPhase phase) where T : DatabaseBackupInternalCommand, new()
        {
            var template = new T();
            var id = Guid.NewGuid();
            return (T)(template with
            {
                CommandId = id,
                EntityId = command.EntityId,
                Subject = new(ActorType.Command, DatabaseBackupCommandRoute.Backup, template.Verb, command.EntityId.Format()),
                Source = new()
                {
                    SourceEventId = id,
                    OperationId = command.EntityId,
                    Source = command.Source,
                    ProtectionSetId = command.ProtectionSetId!.Value,
                    PolicyRevision = command.ExpectedPolicyRevision,
                    OperationKind = DatabaseRecoveryOperationKind.Backup,
                    ProducingHostId = new("backup-bdd-host"),
                    Phase = phase,
                    SourceRevisionOrSequence = sequence,
                    CorrelationId = command.Request.CorrelationId,
                    CausationId = command.CommandId,
                    ObservedUtc = DateTimeOffset.UtcNow
                }
            });
        }
    }

    private static RequestDatabaseBackupCommand BackupRequest()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var request = new DatabaseRequestEnvelope
        {
            RequestId = operationId.Value,
            CallerIdentity = "operator",
            AuthorizationReference = "approval",
            CallerRoles = ["DatabaseRecoveryOperator"],
            Origin = DatabaseRequestOrigin.Console,
            CorrelationId = Guid.NewGuid(),
            EnvironmentIdentity = "paper-trading",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        return new RequestDatabaseBackupCommand
        {
            CommandId = request.RequestId,
            EntityId = operationId,
            Subject = new(ActorType.Command, DatabaseBackupCommandRoute.Backup, RequestDatabaseBackupCommand.VerbValue, operationId.Format()),
            Request = request,
            Source = BackupSource.LocalWorkstation,
            ProtectionSetId = new DatabaseProtectionSetId("core"),
            ConsistencyMode = DatabaseConsistencyMode.CoordinatedProtectionSet,
            RequiredDestinations = [new DatabaseLogicalDestination("vault", true)],
            ExpectedPolicyRevision = 1
        };
    }
}
