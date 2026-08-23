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

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Aws_failure_is_deferred_while_local_operation_continues_in_the_same_dispatch_cycle()
    {
        var aws = Operation(BackupSource.AwsCloud);
        var local = Operation(BackupSource.LocalWorkstation);
        var journal = new RecoverableJournal([aws, local]);
        var executor = new SourceIsolatedExecutor();
        var dispatcher = new DatabaseBackupExecutionDispatcher(journal, executor, new DatabaseBackupHostOptions
        {
            FailedOperationRetryDelay = TimeSpan.FromMinutes(5)
        }, NullLogger<DatabaseBackupExecutionDispatcher>.Instance);

        (await dispatcher.DispatchOnceAsync(CancellationToken.None)).Should().Be(1);

        executor.AwsAttempts.Should().Be(1);
        executor.LocalAttempts.Should().Be(1);
    }

    static RecoverableJournalOperation Operation(BackupSource source = BackupSource.LocalWorkstation)
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
                    Source = source,
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

    sealed class SourceIsolatedExecutor : IDatabaseRecoveryOperationExecutor
    {
        public int AwsAttempts { get; private set; }
        public int LocalAttempts { get; private set; }

        public ValueTask ExecuteAsync(RecoverableJournalOperation operation, CancellationToken cancellationToken)
        {
            if (operation.Intent.Source == BackupSource.AwsCloud)
            {
                AwsAttempts++;
                throw new IOException("simulated AWS source degradation");
            }
            LocalAttempts++;
            return ValueTask.CompletedTask;
        }
    }

    sealed class RecoverableJournal : IDatabaseBackupExecutionJournal
    {
        readonly RecoverableJournalOperation[] _operations;

        public RecoverableJournal(RecoverableJournalOperation operation) : this([operation]) { }

        public RecoverableJournal(RecoverableJournalOperation[] operations) => _operations = operations;

        public async IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            foreach (var operation in _operations) yield return operation;
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
