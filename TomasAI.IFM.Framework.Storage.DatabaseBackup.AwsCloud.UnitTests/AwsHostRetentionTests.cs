using NSubstitute;
using TomasAI.IFM.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Processing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsHostRetentionTests
{
    static readonly DateTimeOffset Now = new(2026, 10, 10, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Disabled_schedule_produces_no_intents()
        => Assert.Empty(AwsAutomaticRetentionSchedule.Create(new AwsCloudDatabaseBackupOptions(), Now));

    [Fact]
    public void Schedule_is_restart_stable_and_scoped_by_replica_and_protection_set()
    {
        var options = Options();
        var first = AwsAutomaticRetentionSchedule.Create(options, Now);
        var again = AwsAutomaticRetentionSchedule.Create(options, Now.AddMinutes(20));
        Assert.Equal(4, first.Length);
        Assert.Equal(first.Select(x => x.OperationId), again.Select(x => x.OperationId));
        Assert.Equal(4, first.Select(x => x.OperationId).Distinct().Count());
        Assert.All(first, x => { x.Validate(); Assert.Single(x.ExecutionEvent.RequiredDestinations); });
        Assert.All(first, x => Assert.Equal(new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero), x.ExecutionEvent.EvaluationBoundaryUtc));
        Assert.NotEqual(first[0].OperationId, AwsAutomaticRetentionSchedule.Create(options, Now.AddDays(1))[0].OperationId);
    }

    [Fact]
    public void Automatic_deletion_is_not_enabled_by_Production_configuration()
    {
        var options = Options();
        options.Environment = AwsBackupEnvironment.Production;
        Assert.Throws<InvalidOperationException>(() => AwsAutomaticRetentionSchedule.Create(options, Now));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Evaluation_and_automatic_execution_reach_host_service_events(bool automatic)
    {
        var fixture = Fixture(automatic);
        await fixture.Orchestrator.ExecuteAsync(fixture.Operation, CancellationToken.None);
        await fixture.Retention.Received(1).CreatePlanAsync(Arg.Is<DatabaseRetentionEvaluationRequest>(x =>
            x.PolicyRevision == 1 && x.ProtectionSetId!.Value.Value == "postgresql-core"), Arg.Any<CancellationToken>());
        await fixture.Retention.Received(automatic ? 1 : 0).ExecuteAsync(Arg.Any<DatabaseRetentionExecutionRequest>(), Arg.Any<CancellationToken>());
        Assert.Contains(fixture.Events, x => x.Event is DatabaseRetentionPlanCreatedEvent);
        Assert.Equal(automatic, fixture.Events.Any(x => x.Event is DatabaseRetentionExecutionCompletedEvent));
        await fixture.Journal.Received(1).RecordCheckpointAsync(Arg.Is<JournalCheckpoint>(x => x.Terminal && x.Phase == DatabaseRecoveryPhase.Completed), Arg.Any<CancellationToken>());
        await fixture.PostgreSql.DidNotReceiveWithAnyArgs().CreateBaseBackupAsync(default!, default!, default);
    }

    [Fact]
    public async Task Deletion_failure_is_terminal_and_reports_failure_instead_of_retrying_partial_deletion()
    {
        var fixture = Fixture(true);
        fixture.Retention.ExecuteAsync(Arg.Any<DatabaseRetentionExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException<DatabaseRetentionExecutionResult>(new UnauthorizedAccessException("denied")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Orchestrator.ExecuteAsync(fixture.Operation, CancellationToken.None).AsTask());
        Assert.Contains(fixture.Events, x => x.Event is DatabaseRetentionExecutionFailedEvent);
        Assert.DoesNotContain(fixture.Events, x => x.Event is DatabaseRetentionExecutionCompletedEvent);
        await fixture.Journal.Received(1).RecordCheckpointAsync(Arg.Is<JournalCheckpoint>(x => x.Terminal && x.Phase == DatabaseRecoveryPhase.Failed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Explicit_execution_uses_requested_revision_and_authorization()
    {
        var fixture = Fixture(false);
        var evaluation = fixture.Operation.Intent.ExecutionEvent;
        var intent = new DatabaseExecutionIntent
        {
            ExecutionEvent = new Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution.DatabaseRetentionExecutionRequestedEvent
            {
                Id = evaluation.Id, EntityId = evaluation.EntityId, CommandId = evaluation.CommandId,
                Source = evaluation.Source, Request = evaluation.Request, Subject = evaluation.Subject,
                RetentionPlanId = evaluation.RetentionPlanId, RetentionPlanRevision = 7,
                RequiredDestinations = evaluation.RequiredDestinations
            }
        };
        await fixture.Orchestrator.ExecuteAsync(fixture.Operation with { Intent = intent }, CancellationToken.None);
        await fixture.Retention.Received(1).ExecuteAsync(Arg.Is<DatabaseRetentionExecutionRequest>(x =>
            x.Revision == 7 && x.ApprovalReference == "DevelopmentOwnerAutomaticRetention"), Arg.Any<CancellationToken>());
        await fixture.Retention.DidNotReceiveWithAnyArgs().CreatePlanAsync(default!, default);
    }

    [Fact]
    public async Task Interrupted_retention_is_not_blindly_reexecuted_after_restart()
    {
        var fixture = Fixture(true);
        await fixture.Orchestrator.ExecuteAsync(fixture.Operation with { LastServiceSequence = 2 }, CancellationToken.None);
        await fixture.Retention.DidNotReceiveWithAnyArgs().CreatePlanAsync(default!, default);
        await fixture.Retention.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default);
        Assert.Contains(fixture.Events, x => x.Event is DatabaseRetentionExecutionFailedEvent
            && x.Event.SafeDiagnosticReference == "retention-interrupted-reconciliation-required");
    }
    static TestFixture Fixture(bool automatic)
    {
        var intent = AwsAutomaticRetentionSchedule.Create(Options(), Now)[0];
        var journal = Substitute.For<IDatabaseBackupExecutionJournal>();
        journal.TryAcquireLeaseAsync(Arg.Any<DatabaseRecoveryOperationId>(), Arg.Any<DatabaseBackupHostId>(),
            Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new JournalLease(intent.OperationId, new DatabaseBackupHostId("test-host"), 1, Now.AddMinutes(2), TimeSpan.FromMinutes(2)));
        var events = new List<DatabaseServiceEventEnvelope>();
        journal.EnqueueServiceEventAsync(Arg.Any<DatabaseServiceEventEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(call => { events.Add(call.Arg<DatabaseServiceEventEnvelope>()); return ValueTask.CompletedTask; });
        var retention = Substitute.For<IDatabaseRetentionCapability>();
        retention.CreatePlanAsync(Arg.Any<DatabaseRetentionEvaluationRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var r = call.Arg<DatabaseRetentionEvaluationRequest>();
                return ValueTask.FromResult(new DatabaseRetentionPlan(1, r.PlanId, r.Revision, r.ReplicaId, r.EvaluationBoundaryUtc, Now, [], []));
            });
        retention.ExecuteAsync(Arg.Any<DatabaseRetentionExecutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var r = call.Arg<DatabaseRetentionExecutionRequest>();
                return ValueTask.FromResult(new DatabaseRetentionExecutionResult(r.PlanId, r.Revision, 0, 0, 0));
            });
        var postgres = Substitute.For<IPostgreSqlBackupCapability>();
        var orchestrator = new DatabaseRecoveryOperationOrchestrator(BackupSource.AwsCloud, journal, postgres,
            Substitute.For<IScyllaBackupCapability>(), Substitute.For<IDatabaseRecoveryEngineSelector>(),
            new DatabaseBackupHostOptions { HostId = "test-host" },
            Substitute.For<IDatabaseBackupPublicationCapability>(), Substitute.For<IDatabaseRestoreSourceCapability>(),
            Substitute.For<IDatabaseRecoveryEvidenceStore>(), Substitute.For<IDatabaseBackupChainPlanner>(),
            retention, automatic ? "DevelopmentOwnerAutomaticRetention" : "");
        return new(orchestrator, new RecoverableJournalOperation(intent, DatabaseRecoveryPhase.Authorized, 1, 0),
            retention, journal, postgres, events);
    }

    sealed record TestFixture(DatabaseRecoveryOperationOrchestrator Orchestrator,
        RecoverableJournalOperation Operation, IDatabaseRetentionCapability Retention,
        IDatabaseBackupExecutionJournal Journal, IPostgreSqlBackupCapability PostgreSql,
        List<DatabaseServiceEventEnvelope> Events);

    static AwsCloudDatabaseBackupOptions Options() => new()
    {
        Enabled = true, AcceptBackupRequests = true, AutomaticRetentionEnabled = true, LiveAwsTestsEnabled = true,
        AutomaticRetentionApprovalReference = "DevelopmentOwnerAutomaticRetention",
        WorkloadAccountId = "107651266250", PrimaryVaultAccountId = "107651266250", RecoveryVaultAccountId = "107651266250",
        PrimaryBucketName = "ifm-primary-development", RecoveryBucketName = "ifm-recovery-development",
        JournalTableName = "ifm-test-journal",
        UploadRoleArn = "arn:aws:iam::107651266250:role/upload",
        RecoveryReadRoleArn = "arn:aws:iam::107651266250:role/recovery",
        PrimaryEncryptionKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/primary",
        RecoveryEncryptionKeyArn = "arn:aws:kms:ca-west-1:107651266250:key/recovery",
        SigningKeyArn = "arn:aws:kms:ca-central-1:107651266250:key/signing"
    };
}