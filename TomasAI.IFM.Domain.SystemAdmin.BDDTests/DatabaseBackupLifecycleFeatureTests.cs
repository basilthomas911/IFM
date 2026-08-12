using FluentAssertions;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;

namespace TomasAI.IFM.Domain.SystemAdmin.BDDTests;

public sealed class DatabaseBackupLifecycleFeatureTests
{
    [Fact]
    public void Given_an_authorized_backup_request_when_core_accepts_it_then_durable_execution_intent_is_recorded()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var request = new DatabaseRequestEnvelope
        {
            RequestId = operationId.Value, CallerIdentity = "operator", AuthorizationReference = "approval",
            CallerRoles = ["DatabaseRecoveryOperator"], Origin = DatabaseRequestOrigin.Console,
            CorrelationId = Guid.NewGuid(), EnvironmentIdentity = "paper-trading", CreatedUtc = DateTimeOffset.UtcNow
        };
        var command = new RequestDatabaseBackupCommand
        {
            CommandId = request.RequestId, EntityId = operationId, Request = request,
            Source = BackupSource.LocalWorkstation, ProtectionSetId = new DatabaseProtectionSetId("core"),
            ConsistencyMode = DatabaseConsistencyMode.CoordinatedProtectionSet,
            RequiredDestinations = [new DatabaseLogicalDestination("vault", true)], ExpectedPolicyRevision = 1
        };
        var state = new DatabaseBackupCommandState();

        var acceptedOperationId = state.Execute(command);

        acceptedOperationId.Should().Be(operationId);
        state.Events.Should().ContainInOrder(
            state.Events.OfType<DatabaseBackupRequestedDomainEvent>().Single(),
            state.Events.OfType<DatabaseBackupAuthorizedDomainEvent>().Single(),
            state.Events.OfType<DatabaseBackupExecutionRequestedDomainEvent>().Single());
        state.Operation.Source.Should().Be(BackupSource.LocalWorkstation);
        state.Operation.PolicyRevision.Should().Be(1);
    }
}
