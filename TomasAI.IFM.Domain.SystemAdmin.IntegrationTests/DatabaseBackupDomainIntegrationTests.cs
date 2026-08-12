using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;

namespace TomasAI.IFM.Domain.SystemAdmin.IntegrationTests;

public sealed class DatabaseBackupDomainIntegrationTests
{
    [Fact]
    public void Committed_backup_intent_maps_to_complete_host_work_order()
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
            RequiredDestinations = [new DatabaseLogicalDestination("vault", true)], ExpectedPolicyRevision = 7
        };
        var state = new DatabaseBackupCommandState();

        state.Execute(command);
        var committedIntent = Assert.IsType<DatabaseBackupExecutionRequestedDomainEvent>(state.Events.Last());
        var workOrder = Assert.IsType<DatabaseBackupExecutionRequestedEvent>(DatabaseBackupStateRepository.ToExecutionEvent(committedIntent));

        Assert.Equal(operationId, workOrder.EntityId);
        Assert.Equal(BackupSource.LocalWorkstation, workOrder.Source.Source);
        Assert.Equal(7, workOrder.Source.PolicyRevision);
        Assert.Single(workOrder.RequiredDestinations);
        Assert.Equal("vault", workOrder.RequiredDestinations[0].Name);
        Assert.Equal(committedIntent.Id, workOrder.Id);
    }
}
