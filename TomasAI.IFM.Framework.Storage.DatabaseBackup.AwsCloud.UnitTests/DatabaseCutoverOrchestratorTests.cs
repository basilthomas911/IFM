using NSubstitute;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Processing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class DatabaseCutoverOrchestratorTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task Completion_requires_all_three_health_conditions(bool database, bool application, bool admission)
    {
        var fixture = Fixture(new(database, application, admission, "recovery-proof"));
        if (database && application && admission)
            await fixture.Orchestrator.ExecuteAsync(fixture.Operation, default);
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Orchestrator.ExecuteAsync(fixture.Operation, default).AsTask());
        Assert.Single(fixture.Events);
        Assert.Equal(6, fixture.Events[0].Event.Source.SourceRevisionOrSequence);
        Assert.Equal(fixture.Operation.Intent.ExecutionEvent.CommandId, fixture.Events[0].Event.CommandId);
        Assert.Equal(database && application && admission, fixture.Events[0].Event is DatabaseRestoreServiceCompletedEvent);
        Assert.Equal(database && application && admission, fixture.Checkpoints.Last().Phase == DatabaseRecoveryPhase.Completed);
        Assert.True(fixture.Checkpoints.Last().Terminal);
    }

    [Fact]
    public async Task Restart_after_durable_completion_finishes_checkpoint_without_replacing_data_again()
    {
        var fixture = Fixture(new(true, true, true, "recovery-proof"));
        fixture.Journal.ReadLatestServiceEventAsync(fixture.Operation.Intent.OperationId, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<Domain.SystemAdmin.Shared.DatabaseBackup.Events.DatabaseBackupServiceEventContract?>(
                Application.DatabaseBackup.Policies.DatabaseBackupServiceEventFactory.Completed(
                    fixture.Operation.Intent, new("test-host"), 6)));
        await fixture.Orchestrator.ExecuteAsync(fixture.Operation with { LastServiceSequence = 6 }, default);
        await fixture.Capability.DidNotReceiveWithAnyArgs().RecoverAsync(default!, default!, default);
        Assert.Empty(fixture.Events);
        Assert.Equal(DatabaseRecoveryPhase.Completed, fixture.Checkpoints.Single().Phase);
    }

    [Fact]
    public async Task Missing_deployment_controller_is_a_terminal_failure_without_native_restore_or_success()
    {
        var fixture = Fixture(new(true, true, true, "proof"), configured: false);
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.Orchestrator.ExecuteAsync(fixture.Operation, default).AsTask());
        Assert.IsType<DatabaseRestoreServiceFailedEvent>(fixture.Events.Single().Event);
        await fixture.Capability.DidNotReceiveWithAnyArgs().RecoverAsync(default!, default!, default);
    }

    static TestFixture Fixture(DatabaseCutoverResult result, bool configured = true)
    {
        var id = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var intent = new DatabaseExecutionIntent
        {
            ExecutionEvent = new DatabaseCutoverExecutionRequestedEvent
            {
                Id = Guid.NewGuid(), EntityId = id, CommandId = Guid.NewGuid(),
                Source = new() { OperationId = id, OperationKind = DatabaseRecoveryOperationKind.Cutover,
                    Source = BackupSource.AwsCloud, ProtectionSetId = new("core-postgresql") },
                RestorePointId = new("verified-backup"), FreshTarget = new("verified-fresh", "disposable"), ValidationRevision = 12
            }
        };
        var journal = Substitute.For<IDatabaseBackupExecutionJournal>();
        journal.TryAcquireLeaseAsync(id, Arg.Any<DatabaseBackupHostId>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(new JournalLease(id, new("test-host"), 2, DateTimeOffset.UtcNow.AddMinutes(2), TimeSpan.FromMinutes(2)));
        var events = new List<DatabaseServiceEventEnvelope>();
        journal.EnqueueServiceEventAsync(Arg.Any<DatabaseServiceEventEnvelope>(), Arg.Any<CancellationToken>())
            .Returns(call => { events.Add(call.Arg<DatabaseServiceEventEnvelope>()); return ValueTask.CompletedTask; });
        var checkpoints = new List<JournalCheckpoint>();
        journal.RecordCheckpointAsync(Arg.Any<JournalCheckpoint>(), Arg.Any<CancellationToken>())
            .Returns(call => { checkpoints.Add(call.Arg<JournalCheckpoint>()); return ValueTask.CompletedTask; });
        var capability = Substitute.For<IDatabaseCutoverCapability>();
        capability.RecoverAsync(Arg.Any<DatabaseCutoverRequest>(), Arg.Any<IProgress<DatabaseNativeProgress>>(), Arg.Any<CancellationToken>())
            .Returns(result);
        var selector = Substitute.For<IDatabaseRecoveryEngineSelector>();
        selector.Select(Arg.Any<DatabaseProtectionSetId>()).Returns(DatabaseEngine.PostgreSql);
        var orchestrator = new DatabaseRecoveryOperationOrchestrator(BackupSource.AwsCloud, journal,
            Substitute.For<IPostgreSqlBackupCapability>(), Substitute.For<IScyllaBackupCapability>(), selector,
            new() { HostId = "test-host" }, Substitute.For<IDatabaseBackupPublicationCapability>(),
            Substitute.For<IDatabaseRestoreSourceCapability>(), Substitute.For<IDatabaseRecoveryEvidenceStore>(),
            Substitute.For<IDatabaseBackupChainPlanner>(), cutover: configured ? capability : null);
        return new(orchestrator, new(intent, DatabaseRecoveryPhase.CuttingOver, 5, 1), journal, capability, events, checkpoints);
    }

    sealed record TestFixture(DatabaseRecoveryOperationOrchestrator Orchestrator, RecoverableJournalOperation Operation,
        IDatabaseBackupExecutionJournal Journal, IDatabaseCutoverCapability Capability,
        List<DatabaseServiceEventEnvelope> Events, List<JournalCheckpoint> Checkpoints);
}
