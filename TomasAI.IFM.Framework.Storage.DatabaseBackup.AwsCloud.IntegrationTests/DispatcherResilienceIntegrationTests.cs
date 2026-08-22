using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Api.DatabaseBackup.Host.Services;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class DispatcherResilienceIntegrationTests
{
    [Fact]
    public async Task Unavailable_vault_is_deferred_without_terminating_or_exception_spamming_the_host()
    {
        var operation = Operation();
        var journal = new RecoverableJournal(operation);
        var executor = new FailingExecutor();
        var dispatcher = new DatabaseBackupExecutionDispatcher(journal, executor, new DatabaseBackupHostOptions
        {
            FailedOperationRetryDelay = TimeSpan.FromMinutes(5)
        }, NullLogger<DatabaseBackupExecutionDispatcher>.Instance);

        (await dispatcher.DispatchOnceAsync(CancellationToken.None)).Should().Be(0);
        (await dispatcher.DispatchOnceAsync(CancellationToken.None)).Should().Be(0);
        executor.Attempts.Should().Be(1);
    }

    static RecoverableJournalOperation Operation()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var eventId = Guid.NewGuid();
        var intent = new DatabaseExecutionIntent
        {
            ExecutionEvent = new DatabaseBackupExecutionRequestedEvent
            {
                Id = eventId,
                EventId = 1,
                CommandId = Guid.NewGuid(),
                EntityId = operationId,
                AggregateId = operationId.Format(),
                EventSource = "test",
                ReceivedOn = DateTime.UtcNow,
                RequiredDestinations = [new("online-vault", true)],
                Source = new()
                {
                    SourceEventId = eventId,
                    OperationId = operationId,
                    Source = BackupSource.LocalWorkstation,
                    ProtectionSetId = new("core-postgresql"),
                    PolicyRevision = 1,
                    OperationKind = DatabaseRecoveryOperationKind.Backup,
                    Phase = DatabaseRecoveryPhase.Requested,
                    CorrelationId = Guid.NewGuid(),
                    CausationId = Guid.NewGuid(),
                    ObservedUtc = DateTimeOffset.UtcNow
                }
            }
        };
        return new(intent, DatabaseRecoveryPhase.Requested, 0, 0);
    }

    sealed class FailingExecutor : IDatabaseRecoveryOperationExecutor
    {
        public int Attempts { get; private set; }
        public ValueTask ExecuteAsync(RecoverableJournalOperation operation, CancellationToken cancellationToken)
        {
            Attempts++;
            throw new DirectoryNotFoundException("The configured vault is unavailable.");
        }
    }

    sealed class RecoverableJournal(RecoverableJournalOperation operation) : IDatabaseBackupExecutionJournal
    {
        public async IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return operation;
        }
        public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask VerifyIntegrityAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask<JournalAdmissionResult> AdmitAsync(DatabaseExecutionIntent intent, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<JournalLease?> TryAcquireLeaseAsync(DatabaseRecoveryOperationId operationId, DatabaseBackupHostId hostId, TimeSpan leaseDuration, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask RenewLeaseAsync(JournalLease lease, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask RecordCheckpointAsync(JournalCheckpoint checkpoint, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask EnqueueServiceEventAsync(DatabaseServiceEventEnvelope envelope, CancellationToken cancellationToken) => throw new NotSupportedException();
        public async IAsyncEnumerable<PendingServiceEvent> ReadPendingServiceEventsAsync(int maximumCount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken) { await Task.CompletedTask; yield break; }
        public ValueTask MarkServiceEventPublishedAsync(Guid eventId, DateTimeOffset publishedUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask MarkCoreAcknowledgedAsync(DatabaseRecoveryOperationId operationId, long domainRevision, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
