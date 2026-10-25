using TomasAI.IFM.Api.DatabaseBackup.Host.Services;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class AwsRuntimeWorkSelectionTests
{
    [Theory]
    [InlineData(DatabaseRecoveryOperationKind.Backup)]
    [InlineData(DatabaseRecoveryOperationKind.Restore)]
    [InlineData(DatabaseRecoveryOperationKind.Cutover)]
    public void Retention_stays_admitted_until_other_work_completes(DatabaseRecoveryOperationKind kind)
    {
        var retention = Operation(DatabaseRecoveryOperationKind.Retention);
        var pending = Operation(kind);
        var nativeWork = AwsDatabaseBackupRuntimeService.SelectRunnableOperations([retention, pending], false);
        Assert.DoesNotContain(retention, nativeWork);
        Assert.Equal(kind != DatabaseRecoveryOperationKind.Cutover, nativeWork.Contains(pending));
        Assert.Equal(DatabaseRecoveryPhase.Admitted, retention.Phase);
        Assert.Equal(1, retention.LastServiceSequence);
        // The same admitted request becomes runnable on the next idle journal scan.
        Assert.Same(retention, Assert.Single(AwsDatabaseBackupRuntimeService.SelectRunnableOperations([retention], false)));
    }

    [Fact]
    public void Cutover_worker_runs_only_cutover_while_native_worker_runs_backups_and_restores()
    {
        var backup = Operation(DatabaseRecoveryOperationKind.Backup);
        var restore = Operation(DatabaseRecoveryOperationKind.Restore);
        var cutover = Operation(DatabaseRecoveryOperationKind.Cutover);
        var retention = Operation(DatabaseRecoveryOperationKind.Retention);
        RecoverableJournalOperation[] pending = [retention, backup, restore, cutover];
        Assert.Equal(new[] { backup, restore }, AwsDatabaseBackupRuntimeService.SelectRunnableOperations(pending, false));
        Assert.Equal(new[] { cutover }, AwsDatabaseBackupRuntimeService.SelectRunnableOperations(pending, true));
        Assert.Empty(AwsDatabaseBackupRuntimeService.SelectRunnableOperations([retention], true));
    }

    static RecoverableJournalOperation Operation(DatabaseRecoveryOperationKind kind)
    {
        var id = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var eventId = Guid.NewGuid();
        return new(new DatabaseExecutionIntent
        {
            ExecutionEvent = new DatabaseBackupExecutionRequestedEvent
            {
                Id = eventId, EventId = 1, CommandId = Guid.NewGuid(), EntityId = id,
                AggregateId = id.Format(), EventSource = "RuntimeWorkSelection", ReceivedOn = DateTime.UtcNow,
                RequiredDestinations = [new DatabaseLogicalDestination("aws-primary", true)],
                Source = new DatabaseSourceEnvelope
                {
                    SourceEventId = eventId, OperationId = id, Source = BackupSource.AwsCloud,
                    ProtectionSetId = new DatabaseProtectionSetId("core-postgresql"), PolicyRevision = 0,
                    OperationKind = kind, Phase = DatabaseRecoveryPhase.Requested,
                    CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow
                }
            }
        }, DatabaseRecoveryPhase.Admitted, 1, 0);
    }
}