using Amazon;
using Amazon.DynamoDBv2;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Journal;
using Xunit.Abstractions;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class LiveAwsJournalIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    public async Task Deployed_development_journal_satisfies_the_shared_admission_lease_checkpoint_and_outbox_contract()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;
        var options = LiveOptions();
        using var client = new AmazonDynamoDBClient(RegionEndpoint.CACentral1);
        var first = new DynamoDbDatabaseBackupExecutionJournal(client, options,
            new DatabaseBackupHostOptions { HostId = "gate5-live-worker-a" }, TimeProvider.System);
        await first.InitializeAsync(CancellationToken.None);
        var intent = Intent();

        var admitted = await first.AdmitAsync(intent, CancellationToken.None);
        var duplicate = await first.AdmitAsync(intent, CancellationToken.None);
        var lease = await first.TryAcquireLeaseAsync(intent.OperationId,
            new DatabaseBackupHostId("gate5-live-worker-a"), TimeSpan.FromMinutes(1), CancellationToken.None);
        var competing = await first.TryAcquireLeaseAsync(intent.OperationId,
            new DatabaseBackupHostId("gate5-live-worker-b"), TimeSpan.FromMinutes(1), CancellationToken.None);

        admitted.Outcome.Should().Be(JournalAdmissionOutcome.Admitted);
        duplicate.Outcome.Should().Be(JournalAdmissionOutcome.ExactDuplicate);
        lease.Should().NotBeNull();
        competing.Should().BeNull();
        await first.RecordCheckpointAsync(new JournalCheckpoint(
            intent.OperationId, lease!.HostId, lease.FencingToken, DatabaseRecoveryPhase.Completed,
            Terminal: true, "gate5-live-qualified", DateTimeOffset.UtcNow), CancellationToken.None);
        await first.MarkCoreAcknowledgedAsync(intent.OperationId, 1, CancellationToken.None);
        var pending = new List<PendingServiceEvent>();
        await foreach (var item in first.ReadPendingServiceEventsAsync(100, CancellationToken.None))
            if (item.OperationId == intent.OperationId) pending.Add(item);
        pending.Should().ContainSingle();
        await first.MarkServiceEventPublishedAsync(pending[0].EventId, DateTimeOffset.UtcNow, CancellationToken.None);
        var recoverable = new List<RecoverableJournalOperation>();
        await foreach (var item in first.ReadRecoverableOperationsAsync(CancellationToken.None))
            if (item.Intent.OperationId == intent.OperationId) recoverable.Add(item);
        recoverable.Should().BeEmpty();
        output.WriteLine("ContractOperationId={0}", intent.OperationId.Format());
    }

    [Fact]
    [Trait("Category", "LiveAwsMutation")]
    public async Task Deployed_development_journal_recovers_a_durable_checkpoint_and_pending_outbox_after_restart()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("IFM_AWS_LIVE_TESTS"), "1", StringComparison.Ordinal))
            return;
        var options = LiveOptions();
        using var client = new AmazonDynamoDBClient(RegionEndpoint.CACentral1);
        var beforeRestart = new DynamoDbDatabaseBackupExecutionJournal(client, options,
            new DatabaseBackupHostOptions { HostId = "gate5-live-restart-worker" }, TimeProvider.System);
        await beforeRestart.InitializeAsync(CancellationToken.None);
        var intent = Intent();
        await beforeRestart.AdmitAsync(intent, CancellationToken.None);
        var firstLease = await beforeRestart.TryAcquireLeaseAsync(intent.OperationId,
            new DatabaseBackupHostId("gate5-live-restart-worker"), TimeSpan.FromMinutes(1), CancellationToken.None);
        firstLease.Should().NotBeNull();
        await beforeRestart.RecordCheckpointAsync(new JournalCheckpoint(
            intent.OperationId, firstLease!.HostId, firstLease.FencingToken, DatabaseRecoveryPhase.Started,
            Terminal: false, "gate5-live-before-restart", DateTimeOffset.UtcNow), CancellationToken.None);

        var afterRestart = new DynamoDbDatabaseBackupExecutionJournal(client, options,
            new DatabaseBackupHostOptions { HostId = "gate5-live-restart-worker" }, TimeProvider.System);
        await afterRestart.InitializeAsync(CancellationToken.None);
        var recoverable = new List<RecoverableJournalOperation>();
        await foreach (var item in afterRestart.ReadRecoverableOperationsAsync(CancellationToken.None))
            if (item.Intent.OperationId == intent.OperationId) recoverable.Add(item);
        recoverable.Should().ContainSingle();
        recoverable[0].Phase.Should().Be(DatabaseRecoveryPhase.Started);
        recoverable[0].FencingToken.Should().Be(firstLease.FencingToken);

        var pendingBeforePublish = new List<PendingServiceEvent>();
        await foreach (var item in afterRestart.ReadPendingServiceEventsAsync(100, CancellationToken.None))
            if (item.OperationId == intent.OperationId) pendingBeforePublish.Add(item);
        pendingBeforePublish.Should().ContainSingle();

        var resumedLease = await afterRestart.TryAcquireLeaseAsync(intent.OperationId,
            new DatabaseBackupHostId("gate5-live-restart-worker"), TimeSpan.FromMinutes(1), CancellationToken.None);
        resumedLease.Should().NotBeNull();
        resumedLease!.FencingToken.Should().Be(firstLease.FencingToken + 1);
        await afterRestart.MarkServiceEventPublishedAsync(
            pendingBeforePublish[0].EventId, DateTimeOffset.UtcNow, CancellationToken.None);
        await afterRestart.RecordCheckpointAsync(new JournalCheckpoint(
            intent.OperationId, resumedLease.HostId, resumedLease.FencingToken, DatabaseRecoveryPhase.Completed,
            Terminal: true, "gate5-live-after-restart", DateTimeOffset.UtcNow), CancellationToken.None);

        var pendingAfterPublish = new List<PendingServiceEvent>();
        await foreach (var item in afterRestart.ReadPendingServiceEventsAsync(100, CancellationToken.None))
            if (item.OperationId == intent.OperationId) pendingAfterPublish.Add(item);
        pendingAfterPublish.Should().BeEmpty();
        var recoverableAfterCompletion = new List<RecoverableJournalOperation>();
        await foreach (var item in afterRestart.ReadRecoverableOperationsAsync(CancellationToken.None))
            if (item.Intent.OperationId == intent.OperationId) recoverableAfterCompletion.Add(item);
        recoverableAfterCompletion.Should().BeEmpty();
        output.WriteLine("RestartOperationId={0}", intent.OperationId.Format());
        output.WriteLine("InitialFencingToken={0}", firstLease.FencingToken);
        output.WriteLine("ResumedFencingToken={0}", resumedLease.FencingToken);
    }

    static AwsCloudDatabaseBackupOptions LiveOptions() => new()
    {
        Enabled = true, LiveAwsTestsEnabled = true, Environment = AwsBackupEnvironment.Development,
        WorkloadAccountId = "107651266250", PrimaryVaultAccountId = "107651266250", RecoveryVaultAccountId = "107651266250",
        PrimaryRegion = "ca-central-1", RecoveryRegion = "ca-west-1",
        PrimaryBucketName = "ifm-db-backup-development-primary-107651266250",
        RecoveryBucketName = "ifm-db-backup-development-recovery-107651266250",
        JournalTableName = "ifm-database-backup-journal-development",
        UploadRoleArn = "arn:aws:iam::107651266250:role/ifm-database-backup-upload-development",
        RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/ifm-database-backup-recovery-read-development",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/4772d4b1-82d9-49fc-acca-b97e73fe93df",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/4277d9a7-5182-4299-a61a-19ca0c5cf404",
        SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/2edd60e5-be19-483d-b4df-88df45aa2fb2"
    };

    static DatabaseExecutionIntent Intent()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var eventId = Guid.NewGuid();
        return new DatabaseExecutionIntent
        {
            ExecutionEvent = new DatabaseBackupExecutionRequestedEvent
            {
                Id = eventId, EventId = 1, CommandId = Guid.NewGuid(), EntityId = operationId,
                AggregateId = operationId.Format(), EventSource = "Gate5LiveQualification", ReceivedOn = DateTime.UtcNow,
                RequiredDestinations = [new DatabaseLogicalDestination("aws-primary", true)],
                Source = new DatabaseSourceEnvelope
                {
                    SourceEventId = eventId, OperationId = operationId, Source = BackupSource.AwsCloud,
                    ProtectionSetId = new DatabaseProtectionSetId("postgresql-core"), PolicyRevision = 1,
                    OperationKind = DatabaseRecoveryOperationKind.Backup, Phase = DatabaseRecoveryPhase.Requested,
                    CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow
                }
            }
        };
    }
}
