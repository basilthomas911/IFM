using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Processing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.IntegrationTests;

public sealed class AwsProcessorStateMachineIntegrationTests
{
    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Duplicate_admission_is_exact_and_split_brain_worker_does_no_native_work()
    {
        var journal = new RecordingJournal
        {
            AdmissionOutcome = JournalAdmissionOutcome.ExactDuplicate,
            LeaseAvailable = false
        };
        var harness = Create(journal);
        var intent = Intent();

        var admission = await harness.Orchestrator.AdmitAsync(intent, CancellationToken.None);
        await harness.Orchestrator.ExecuteAsync(
            new RecoverableJournalOperation(intent, DatabaseRecoveryPhase.Admitted, 1, 0), CancellationToken.None);

        admission.Outcome.Should().Be(DatabaseExecutionAdmissionOutcome.ExactDuplicate);
        await harness.PostgreSql.DidNotReceive().CreateBaseBackupAsync(
            Arg.Any<PostgreSqlBackupRequest>(), Arg.Any<IProgress<DatabaseNativeProgress>>(), Arg.Any<CancellationToken>());
        await harness.Publication.DidNotReceive().PublishAsync(
            Arg.Any<DatabaseBackupPublicationRequest>(), Arg.Any<CancellationToken>());
        journal.Events.Should().BeEmpty();
        journal.Checkpoints.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Reordered_restart_skips_persisted_events_and_completes_only_after_publication()
    {
        var journal = new RecordingJournal();
        var harness = Create(journal);
        var intent = Intent();

        await harness.Orchestrator.ExecuteAsync(
            new RecoverableJournalOperation(intent, DatabaseRecoveryPhase.Verifying, 4, 1), CancellationToken.None);

        journal.Events.Select(static value => value.Source.SourceRevisionOrSequence).Should().Equal(5, 7);
        journal.Events.Last().GetType().Name.Should().Be("DatabaseBackupServiceCompletedEvent");
        journal.Checkpoints.Should().ContainSingle(static value => value.Terminal)
            .Which.Phase.Should().Be(DatabaseRecoveryPhase.Completed);
        await harness.Publication.Received(1).PublishAsync(
            Arg.Any<DatabaseBackupPublicationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Publication_failure_never_reports_success_or_writes_a_terminal_checkpoint()
    {
        var journal = new RecordingJournal();
        var harness = Create(journal);
        harness.Publication.PublishAsync(
                Arg.Any<DatabaseBackupPublicationRequest>(), Arg.Any<CancellationToken>())
            .Returns<ValueTask<DatabaseBackupPublicationResult>>(_ => throw new IOException("simulated unavailable vault"));
        var intent = Intent();

        var action = () => harness.Orchestrator.ExecuteAsync(
            new RecoverableJournalOperation(intent, DatabaseRecoveryPhase.Admitted, 1, 0), CancellationToken.None).AsTask();

        await action.Should().ThrowAsync<IOException>();
        journal.Events.Should().NotContain(value => value.GetType().Name == "DatabaseBackupServiceCompletedEvent");
        journal.Checkpoints.Should().NotContain(static value => value.Terminal);
        journal.Checkpoints.Select(static value => value.Phase).Should().ContainInOrder(
            DatabaseRecoveryPhase.Started, DatabaseRecoveryPhase.Capturing, DatabaseRecoveryPhase.Verifying);
    }

    [Fact]
    [Trait("Category", "Gate8Integration")]
    public async Task Cancellation_before_lease_acquisition_reaches_no_native_or_publication_boundary()
    {
        var journal = new RecordingJournal();
        var harness = Create(journal);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var intent = Intent();

        var action = () => harness.Orchestrator.ExecuteAsync(
            new RecoverableJournalOperation(intent, DatabaseRecoveryPhase.Admitted, 1, 0), cancellation.Token).AsTask();

        await action.Should().ThrowAsync<OperationCanceledException>();
        await harness.PostgreSql.DidNotReceive().CreateBaseBackupAsync(
            Arg.Any<PostgreSqlBackupRequest>(), Arg.Any<IProgress<DatabaseNativeProgress>>(), Arg.Any<CancellationToken>());
        await harness.Publication.DidNotReceive().PublishAsync(
            Arg.Any<DatabaseBackupPublicationRequest>(), Arg.Any<CancellationToken>());
        journal.Events.Should().BeEmpty();
    }

    static Harness Create(RecordingJournal journal)
    {
        var postgreSql = Substitute.For<IPostgreSqlBackupCapability>();
        var scylla = Substitute.For<IScyllaBackupCapability>();
        var publication = Substitute.For<IDatabaseBackupPublicationCapability>();
        var restoreSources = Substitute.For<IDatabaseRestoreSourceCapability>();
        var evidence = Substitute.For<IDatabaseRecoveryEvidenceStore>();
        var chainPlanner = Substitute.For<IDatabaseBackupChainPlanner>();
        var lineage = new DatabaseBackupLineage
        {
            RequestedMode = DatabaseBackupMode.Full,
            ResolvedMode = DatabaseBackupMode.Full,
            NativeKind = DatabaseNativeBackupKind.PostgreSqlBase,
            BaseRestorePointId = new DatabaseRestorePointId("gate8-base")
        };
        chainPlanner.PlanAsync(Arg.Any<DatabaseBackupPlanningRequest>(), Arg.Any<CancellationToken>())
            .Returns(lineage);
        postgreSql.CreateBaseBackupAsync(
                Arg.Any<PostgreSqlBackupRequest>(), Arg.Any<IProgress<DatabaseNativeProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new PostgreSqlBackupBoundary("gate8-native-boundary") { BackupLineage = lineage });
        postgreSql.VerifyAsync(Arg.Any<PostgreSqlVerificationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PostgreSqlVerificationResult(DatabaseVerificationLevel.Native, true));
        publication.PublishAsync(Arg.Any<DatabaseBackupPublicationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var request = call.Arg<DatabaseBackupPublicationRequest>();
                return new DatabaseBackupPublicationResult(
                    new DatabaseRestorePointId(request.OperationId.Format()),
                    $"manifest-{request.OperationId.Format()}",
                    1,
                    [new DatabaseArtifactReplicaDescriptor
                    {
                        ArtifactId = new DatabaseArtifactId("gate8-artifact"),
                        ReplicaId = new DatabaseArtifactReplicaId("aws-primary"),
                        Engine = DatabaseEngine.PostgreSql,
                        State = DatabaseArtifactReplicaState.Published,
                        SafeDestinationReference = "gate8-primary-version"
                    }]);
            });
        var options = AwsOptions();
        var orchestrator = new DatabaseRecoveryOperationOrchestrator(
            BackupSource.AwsCloud,
            journal,
            postgreSql,
            scylla,
            new AwsDatabaseRecoveryEngineSelector(options),
            new DatabaseBackupHostOptions { HostId = "gate8-worker", LeaseDuration = TimeSpan.FromSeconds(30) },
            publication,
            restoreSources,
            evidence,
            chainPlanner);
        return new Harness(orchestrator, postgreSql, publication);
    }

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
                EventSource = "Gate8Qualification",
                ReceivedOn = DateTime.UtcNow,
                RequiredDestinations = [new DatabaseLogicalDestination("aws-primary", true)],
                Source = new DatabaseSourceEnvelope
                {
                    SourceEventId = eventId,
                    OperationId = operationId,
                    Source = BackupSource.AwsCloud,
                    ProtectionSetId = new DatabaseProtectionSetId("postgresql-core"),
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

    static AwsCloudDatabaseBackupOptions AwsOptions() => new()
    {
        Enabled = true,
        Environment = AwsBackupEnvironment.Development,
        WorkloadAccountId = "107651266250",
        PrimaryVaultAccountId = "107651266250",
        RecoveryVaultAccountId = "107651266250",
        PrimaryRegion = "ca-central-1",
        RecoveryRegion = "ca-west-1",
        PrimaryBucketName = "ifm-primary-development",
        RecoveryBucketName = "ifm-recovery-development",
        JournalTableName = "ifm-database-backup-journal-development",
        UploadRoleArn = "arn:aws:iam::107651266250:role/ifm-upload-development",
        RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/ifm-recovery-development",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/11111111-1111-1111-1111-111111111111",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/22222222-2222-2222-2222-222222222222",
        SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/33333333-3333-3333-3333-333333333333"
    };

    sealed record Harness(
        DatabaseRecoveryOperationOrchestrator Orchestrator,
        IPostgreSqlBackupCapability PostgreSql,
        IDatabaseBackupPublicationCapability Publication);

    sealed class RecordingJournal : IDatabaseBackupExecutionJournal
    {
        public JournalAdmissionOutcome AdmissionOutcome { get; init; } = JournalAdmissionOutcome.Admitted;
        public bool LeaseAvailable { get; init; } = true;
        public List<Domain.SystemAdmin.Shared.DatabaseBackup.Events.DatabaseBackupServiceEventContract> Events { get; } = [];
        public List<JournalCheckpoint> Checkpoints { get; } = [];

        public ValueTask<JournalAdmissionResult> AdmitAsync(
            DatabaseExecutionIntent intent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(new JournalAdmissionResult(intent.OperationId, AdmissionOutcome));
        }

        public ValueTask<JournalLease?> TryAcquireLeaseAsync(
            DatabaseRecoveryOperationId operationId, DatabaseBackupHostId hostId,
            TimeSpan leaseDuration, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<JournalLease?>(LeaseAvailable
                ? new JournalLease(operationId, hostId, 1, DateTimeOffset.UtcNow.Add(leaseDuration), leaseDuration)
                : null);
        }

        public ValueTask RecordCheckpointAsync(JournalCheckpoint checkpoint, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Checkpoints.Add(checkpoint);
            return ValueTask.CompletedTask;
        }

        public ValueTask EnqueueServiceEventAsync(
            DatabaseServiceEventEnvelope envelope, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Events.All(value => value.Id != envelope.Event.Id)) Events.Add(envelope.Event);
            return ValueTask.CompletedTask;
        }

        public ValueTask RenewLeaseAsync(JournalLease lease, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.CompletedTask;
        }

        public ValueTask InitializeAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public ValueTask VerifyIntegrityAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public async IAsyncEnumerable<PendingServiceEvent> ReadPendingServiceEventsAsync(
            int maximumCount, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        { await Task.CompletedTask; yield break; }
        public ValueTask MarkServiceEventPublishedAsync(
            Guid eventId, DateTimeOffset publishedUtc, CancellationToken cancellationToken) => ValueTask.CompletedTask;
        public async IAsyncEnumerable<RecoverableJournalOperation> ReadRecoverableOperationsAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        { await Task.CompletedTask; yield break; }
        public ValueTask MarkCoreAcknowledgedAsync(
            DatabaseRecoveryOperationId operationId, long domainRevision, CancellationToken cancellationToken)
            => ValueTask.CompletedTask;
    }
}
