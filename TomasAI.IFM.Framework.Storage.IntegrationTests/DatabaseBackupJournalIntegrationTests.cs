using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class DatabaseBackupJournalIntegrationTests : IAsyncLifetime
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "ifm-gate5", Guid.NewGuid().ToString("N"));
    string JournalPath => Path.Combine(_directory, "execution-journal.db");

    [Fact]
    public async Task Admission_is_exactly_idempotent_and_rejects_conflicting_content()
    {
        var journal = CreateJournal();
        await journal.InitializeAsync(CancellationToken.None);
        var intent = Intent();

        var admitted = await journal.AdmitAsync(intent, CancellationToken.None);
        var duplicate = await journal.AdmitAsync(intent, CancellationToken.None);
        var conflict = intent with
        {
            ExecutionEvent = intent.ExecutionEvent with { SafeDiagnosticReference = "different-content" }
        };
        Func<Task> conflictingAdmission = async () => await journal.AdmitAsync(conflict, CancellationToken.None);
        var pending = await PendingAsync(journal);

        admitted.Outcome.Should().Be(JournalAdmissionOutcome.Admitted);
        duplicate.Outcome.Should().Be(JournalAdmissionOutcome.ExactDuplicate);
        await conflictingAdmission.Should().ThrowAsync<DatabaseExecutionConflictException>();
        pending.Should().ContainSingle();
        pending[0].ServiceSequence.Should().Be(1);
    }

    [Fact]
    public async Task Restart_recovers_operation_with_new_fence_and_preserves_ordered_outbox()
    {
        var beforeRestart = CreateJournal();
        await beforeRestart.InitializeAsync(CancellationToken.None);
        var intent = Intent();
        await beforeRestart.AdmitAsync(intent, CancellationToken.None);

        var afterRestart = CreateJournal();
        await afterRestart.InitializeAsync(CancellationToken.None);
        await afterRestart.VerifyIntegrityAsync(CancellationToken.None);
        var recoverable = await RecoverableAsync(afterRestart);
        var processor = new LocalWorkstationDatabaseRecoveryProcessor(
            afterRestart,
            new FakePostgreSqlBackupCapability(),
            HostOptions());

        await processor.ExecuteAsync(recoverable.Single(), CancellationToken.None);

        var pending = await PendingAsync(afterRestart);
        pending.Select(static item => item.ServiceSequence).Should().Equal(1, 2, 3, 4, 5, 6);
        pending.Select(static item => item.Event.GetType().Name).Should().Equal(
            "DatabaseBackupServiceAcceptedEvent",
            "DatabaseBackupServiceStartedEvent",
            "DatabaseBackupBoundaryEstablishedEvent",
            "DatabaseBackupVerificationCompletedEvent",
            "DatabaseRecoveryRunStatisticsCapturedEvent",
            "DatabaseBackupServiceCompletedEvent");
        (await RecoverableAsync(afterRestart)).Should().BeEmpty();
    }

    [Fact]
    public async Task Restart_after_outbox_write_before_checkpoint_continues_canonical_sequence()
    {
        var journal = CreateJournal();
        await journal.InitializeAsync(CancellationToken.None);
        var intent = Intent();
        await journal.AdmitAsync(intent, CancellationToken.None);
        var crashJournal = new CrashAfterStartedJournal(journal);
        var firstProcessor = new LocalWorkstationDatabaseRecoveryProcessor(
            crashJournal, new FakePostgreSqlBackupCapability(), HostOptions());

        Func<Task> interrupted = async () =>
            await firstProcessor.ExecuteAsync((await RecoverableAsync(journal)).Single(), CancellationToken.None);
        await interrupted.Should().ThrowAsync<InvalidOperationException>().WithMessage("simulated process termination");

        var restartedProcessor = new LocalWorkstationDatabaseRecoveryProcessor(
            journal, new FakePostgreSqlBackupCapability(), HostOptions());
        await restartedProcessor.ExecuteAsync((await RecoverableAsync(journal)).Single(), CancellationToken.None);

        var pending = await PendingAsync(journal);
        pending.Select(static item => item.ServiceSequence).Should().Equal(1, 2, 3, 4, 5, 6);
        pending.Select(static item => item.Event.GetType().Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Reacquiring_a_lease_invalidates_the_previous_fencing_token()
    {
        var journal = CreateJournal();
        await journal.InitializeAsync(CancellationToken.None);
        var intent = Intent();
        await journal.AdmitAsync(intent, CancellationToken.None);
        var host = new DatabaseBackupHostId("gate5-host");
        var first = await journal.TryAcquireLeaseAsync(intent.OperationId, host, TimeSpan.FromMinutes(1), CancellationToken.None);
        var second = await journal.TryAcquireLeaseAsync(intent.OperationId, host, TimeSpan.FromMinutes(1), CancellationToken.None);
        var staleCheckpoint = new JournalCheckpoint(
            intent.OperationId, host, first!.FencingToken, DatabaseRecoveryPhase.Started,
            false, "stale-worker", DateTimeOffset.UtcNow);

        Func<Task> staleWrite = async () => await journal.RecordCheckpointAsync(staleCheckpoint, CancellationToken.None);

        second!.FencingToken.Should().Be(first.FencingToken + 1);
        await staleWrite.Should().ThrowAsync<DatabaseLeaseLostException>();
    }

    [Fact]
    public void Registry_rejects_none_unknown_and_unregistered_sources()
    {
        var processor = new LocalWorkstationDatabaseRecoveryProcessor(
            CreateJournal(), new FakePostgreSqlBackupCapability(), HostOptions());
        var registry = new DatabaseRecoveryProcessorRegistry([processor]);

        registry.GetRequired(BackupSource.LocalWorkstation).Should().BeSameAs(processor);
        ((Action)(() => registry.GetRequired(BackupSource.None)))
            .Should().Throw<UnsupportedDatabaseBackupSourceException>();
        ((Action)(() => registry.GetRequired((BackupSource)999)))
            .Should().Throw<UnsupportedDatabaseBackupSourceException>();
        ((Action)(() => registry.GetRequired(BackupSource.AwsCloud)))
            .Should().Throw<UnsupportedDatabaseBackupSourceException>();
    }

    [Fact]
    public async Task Production_restore_stops_at_ready_for_cutover_after_fresh_target_validation()
    {
        var journal = CreateJournal();
        await journal.InitializeAsync(CancellationToken.None);
        var intent = RestoreIntent();
        await journal.AdmitAsync(intent, CancellationToken.None);
        var processor = new LocalWorkstationDatabaseRecoveryProcessor(
            journal, new FakePostgreSqlBackupCapability(), HostOptions());

        await processor.ExecuteAsync((await RecoverableAsync(journal)).Single(), CancellationToken.None);

        var pending = await PendingAsync(journal);
        pending.Select(static item => item.ServiceSequence).Should().Equal(1, 2, 3, 4, 5);
        pending.Select(static item => item.Event.GetType().Name).Should().Equal(
            "DatabaseRestoreServiceAcceptedEvent",
            "DatabaseRestoreServiceStartedEvent",
            "DatabaseRestoreValidationCompletedEvent",
            "DatabaseRecoveryRunStatisticsCapturedEvent",
            "DatabaseRestoreReadyForCutoverEvent");
        pending[^1].Event.ValidationRevision.Should().Be(1);
        (await RunStatisticsCountAsync()).Should().Be(1);
        (await RecoverableAsync(journal)).Should().BeEmpty();
    }

    [Fact]
    public async Task Long_native_work_renews_the_fenced_lease_until_checkpoint()
    {
        var journal = CreateJournal();
        await journal.InitializeAsync(CancellationToken.None);
        var intent = Intent();
        await journal.AdmitAsync(intent, CancellationToken.None);
        var options = HostOptions();
        options.LeaseDuration = TimeSpan.FromMilliseconds(150);
        var countingJournal = new CountingRenewalJournal(journal);
        var processor = new LocalWorkstationDatabaseRecoveryProcessor(
            countingJournal, new SlowPostgreSqlCapability(TimeSpan.FromMilliseconds(350)), options);

        await processor.ExecuteAsync((await RecoverableAsync(journal)).Single(), CancellationToken.None);

        (await RecoverableAsync(journal)).Should().BeEmpty();
        (await PendingAsync(journal)).Should().HaveCount(6);
        countingJournal.RenewalCount.Should().BeGreaterThan(0);
    }

    SqliteDatabaseBackupExecutionJournal CreateJournal() => new(
        new DatabaseBackupJournalOptions
        {
            Path = JournalPath,
            RequirePersistentPath = false,
            BusyTimeoutMilliseconds = 2_000
        },
        HostOptions(),
        NullLogger<SqliteDatabaseBackupExecutionJournal>.Instance);

    static LocalWorkstationDatabaseBackupOptions HostOptions() => new()
    {
        HostId = "gate5-host",
        LeaseDuration = TimeSpan.FromMinutes(1),
        PollInterval = TimeSpan.FromMilliseconds(10)
    };

    static DatabaseExecutionIntent Intent()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var eventId = Guid.NewGuid();
        return new DatabaseExecutionIntent
        {
            ExecutionEvent = new DatabaseBackupExecutionRequestedEvent
            {
                Id = eventId,
                EventId = 1,
                CommandId = Guid.NewGuid(),
                EntityId = operationId,
                AggregateId = operationId.Format(),
                EventSource = "DatabaseBackupCommandActor",
                ReceivedOn = DateTime.UtcNow,
                RequiredDestinations = [new DatabaseLogicalDestination("fake-vault", true)],
                Source = new DatabaseSourceEnvelope
                {
                    SourceEventId = eventId,
                    OperationId = operationId,
                    Source = BackupSource.LocalWorkstation,
                    ProtectionSetId = new DatabaseProtectionSetId("gate5-core"),
                    PolicyRevision = 1,
                    OperationKind = DatabaseRecoveryOperationKind.Backup,
                    Phase = DatabaseRecoveryPhase.Requested,
                    CorrelationId = Guid.NewGuid(),
                    CausationId = Guid.NewGuid(),
                    ObservedUtc = DateTimeOffset.UtcNow
                }
            }
        };
    }

    static DatabaseExecutionIntent RestoreIntent()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var eventId = Guid.NewGuid();
        return new DatabaseExecutionIntent
        {
            ExecutionEvent = new DatabaseRestoreExecutionRequestedEvent
            {
                Id = eventId,
                EventId = 1,
                CommandId = Guid.NewGuid(),
                EntityId = operationId,
                AggregateId = operationId.Format(),
                EventSource = "DatabaseBackupCommandActor",
                ReceivedOn = DateTime.UtcNow,
                RestorePointId = new DatabaseRestorePointId("gate6-restore-point"),
                FreshTarget = new DatabaseFreshTargetDescriptor("disposable-validation", "gate6"),
                RestoreClass = DatabaseRestoreClass.ProductionRecovery,
                Source = new DatabaseSourceEnvelope
                {
                    SourceEventId = eventId,
                    OperationId = operationId,
                    Source = BackupSource.LocalWorkstation,
                    ProtectionSetId = new DatabaseProtectionSetId("gate6-core"),
                    PolicyRevision = 1,
                    OperationKind = DatabaseRecoveryOperationKind.Restore,
                    Phase = DatabaseRecoveryPhase.Requested,
                    CorrelationId = Guid.NewGuid(),
                    CausationId = Guid.NewGuid(),
                    ObservedUtc = DateTimeOffset.UtcNow
                }
            }
        };
    }

    static async Task<List<PendingServiceEvent>> PendingAsync(IDatabaseBackupExecutionJournal journal)
    {
        var result = new List<PendingServiceEvent>();
        await foreach (var item in journal.ReadPendingServiceEventsAsync(100, CancellationToken.None)) result.Add(item);
        return result;
    }

    static async Task<List<RecoverableJournalOperation>> RecoverableAsync(IDatabaseBackupExecutionJournal journal)
    {
        var result = new List<RecoverableJournalOperation>();
        await foreach (var item in journal.ReadRecoverableOperationsAsync(CancellationToken.None)) result.Add(item);
        return result;
    }

    async Task<long> RunStatisticsCountAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={JournalPath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM journal_run_stats_v1;";
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        return Task.CompletedTask;
    }

    sealed class CrashAfterStartedJournal(IDatabaseBackupExecutionJournal inner) : IDatabaseBackupExecutionJournal
    {
        bool _crashed;

        public ValueTask InitializeAsync(CancellationToken cancellationToken) => inner.InitializeAsync(cancellationToken);
        public ValueTask VerifyIntegrityAsync(CancellationToken cancellationToken) => inner.VerifyIntegrityAsync(cancellationToken);
        public ValueTask<JournalAdmissionResult> AdmitAsync(DatabaseExecutionIntent intent, CancellationToken cancellationToken) => inner.AdmitAsync(intent, cancellationToken);
        public ValueTask<JournalLease?> TryAcquireLeaseAsync(DatabaseRecoveryOperationId operationId, DatabaseBackupHostId hostId, TimeSpan leaseDuration, CancellationToken cancellationToken) => inner.TryAcquireLeaseAsync(operationId, hostId, leaseDuration, cancellationToken);
        public ValueTask RenewLeaseAsync(JournalLease lease, CancellationToken cancellationToken) => inner.RenewLeaseAsync(lease, cancellationToken);
        public ValueTask RecordCheckpointAsync(JournalCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            if (!_crashed && checkpoint.Phase == DatabaseRecoveryPhase.Started)
            {
                _crashed = true;
                throw new InvalidOperationException("simulated process termination");
            }
            return inner.RecordCheckpointAsync(checkpoint, cancellationToken);
        }
        public ValueTask EnqueueServiceEventAsync(DatabaseServiceEventEnvelope envelope, CancellationToken cancellationToken) => inner.EnqueueServiceEventAsync(envelope, cancellationToken);
        public IAsyncEnumerable<PendingServiceEvent> ReadPendingServiceEventsAsync(int maximumCount, CancellationToken cancellationToken) => inner.ReadPendingServiceEventsAsync(maximumCount, cancellationToken);
        public ValueTask MarkServiceEventPublishedAsync(Guid eventId, DateTimeOffset publishedUtc, CancellationToken cancellationToken) => inner.MarkServiceEventPublishedAsync(eventId, publishedUtc, cancellationToken);
        public IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync(CancellationToken cancellationToken) => inner.ReadRecoverableOperationsAsync(cancellationToken);
        public ValueTask MarkCoreAcknowledgedAsync(DatabaseRecoveryOperationId operationId, long domainRevision, CancellationToken cancellationToken) => inner.MarkCoreAcknowledgedAsync(operationId, domainRevision, cancellationToken);
    }

    sealed class SlowPostgreSqlCapability(TimeSpan delay) : IPostgreSqlBackupCapability
    {
        readonly FakePostgreSqlBackupCapability _inner = new();

        public async ValueTask<PostgreSqlBackupBoundary> CreateBaseBackupAsync(
            PostgreSqlBackupRequest request,
            IProgress<DatabaseNativeProgress> progress,
            CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return await _inner.CreateBaseBackupAsync(request, progress, cancellationToken);
        }

        public async ValueTask<PostgreSqlVerificationResult> VerifyAsync(
            PostgreSqlVerificationRequest request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return await _inner.VerifyAsync(request, cancellationToken);
        }

        public ValueTask<PostgreSqlRestoreResult> RestoreToFreshTargetAsync(
            PostgreSqlRestoreRequest request,
            IProgress<DatabaseNativeProgress> progress,
            CancellationToken cancellationToken)
            => _inner.RestoreToFreshTargetAsync(request, progress, cancellationToken);
    }

    sealed class CountingRenewalJournal(IDatabaseBackupExecutionJournal inner) : IDatabaseBackupExecutionJournal
    {
        public int RenewalCount { get; private set; }
        public ValueTask InitializeAsync(CancellationToken cancellationToken) => inner.InitializeAsync(cancellationToken);
        public ValueTask VerifyIntegrityAsync(CancellationToken cancellationToken) => inner.VerifyIntegrityAsync(cancellationToken);
        public ValueTask<JournalAdmissionResult> AdmitAsync(DatabaseExecutionIntent intent, CancellationToken cancellationToken) => inner.AdmitAsync(intent, cancellationToken);
        public ValueTask<JournalLease?> TryAcquireLeaseAsync(DatabaseRecoveryOperationId operationId, DatabaseBackupHostId hostId, TimeSpan leaseDuration, CancellationToken cancellationToken) => inner.TryAcquireLeaseAsync(operationId, hostId, leaseDuration, cancellationToken);
        public async ValueTask RenewLeaseAsync(JournalLease lease, CancellationToken cancellationToken)
        {
            await inner.RenewLeaseAsync(lease, cancellationToken);
            RenewalCount++;
        }
        public ValueTask RecordCheckpointAsync(JournalCheckpoint checkpoint, CancellationToken cancellationToken) => inner.RecordCheckpointAsync(checkpoint, cancellationToken);
        public ValueTask EnqueueServiceEventAsync(DatabaseServiceEventEnvelope envelope, CancellationToken cancellationToken) => inner.EnqueueServiceEventAsync(envelope, cancellationToken);
        public IAsyncEnumerable<PendingServiceEvent> ReadPendingServiceEventsAsync(int maximumCount, CancellationToken cancellationToken) => inner.ReadPendingServiceEventsAsync(maximumCount, cancellationToken);
        public ValueTask MarkServiceEventPublishedAsync(Guid eventId, DateTimeOffset publishedUtc, CancellationToken cancellationToken) => inner.MarkServiceEventPublishedAsync(eventId, publishedUtc, cancellationToken);
        public IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync(CancellationToken cancellationToken) => inner.ReadRecoverableOperationsAsync(cancellationToken);
        public ValueTask MarkCoreAcknowledgedAsync(DatabaseRecoveryOperationId operationId, long domainRevision, CancellationToken cancellationToken) => inner.MarkCoreAcknowledgedAsync(operationId, domainRevision, cancellationToken);
    }
}
