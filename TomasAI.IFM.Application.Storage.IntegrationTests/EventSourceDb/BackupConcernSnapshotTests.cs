using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SystemAdminDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using Xunit.Abstractions;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class BackupConcernSnapshotTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public Task DatabaseBackup_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupCommandState>((factory, transactions) =>
            new DatabaseBackupStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseBackupSet_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupSetCommandState>((factory, transactions) =>
            new DatabaseBackupSetStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupSetCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupSetStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseBackupPolicy_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupPolicyCommandState>((factory, transactions) =>
            new DatabaseBackupPolicyStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupPolicyCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupPolicyStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseRecovery_restores_operation_and_concern_state() =>
        Verify<DatabaseRecoveryCommandState>((factory, transactions) =>
            new DatabaseRecoveryStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseRecoveryCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseRecoveryStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseBackupRetention_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupRetentionCommandState>((factory, transactions) =>
            new DatabaseBackupRetentionStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupRetentionCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupRetentionStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseBackupService_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupServiceCommandState>((factory, transactions) =>
            new DatabaseBackupServiceStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupServiceCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupServiceStateRepository>.Instance, Databases(), transactions));

    private async Task Verify<TState>(Func<IEventSourceActorStateFactory, IPostgresEventTransaction,
        IEventSourceActorStateRepository<TState>> create) where TState : DatabaseOperationCommandState<TState>, new()
    {
        var id = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var legacy = $"Command.DatabaseBackupCommand.{id.Format()}";
        var command = new RecordDatabaseOperationProgressCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, typeof(TState).Name, "RecordProgress", id.Format()),
            Source = Envelope(129), ProgressPercent = 75
        };
        var source = fixture.ActorEventDb;
        var transactions = new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, source.ConnectionString, "System.Data.Postgres"));
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<TState>().Returns(_ => new TState());
        var repository = create(factory, transactions);
        await source.SaveEventsAsync(legacy, Guid.NewGuid(), new DomainEventCollection([Fact(1)]), 0, CancellationToken.None);
        await source.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection(
            Enumerable.Range(2, 127).Select(i => Fact(i))), 0, CancellationToken.None);
        var pending = await repository.LoadStateAsync(command);
        Assert.Equal(128, pending.Operation.Revision);
        Assert.Equal(127, pending.CommittedStreamVersion);
        Assert.True(pending.Update(Fact(129), command));
        await repository.SaveStateAsync(Substitute.For<ICommandActorContext>(), pending, command);
        Assert.Equal(129, pending.CommittedStreamVersion);

        await source.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([Fact(130)]),
            129, CancellationToken.None);
        var full = new double[20];
        var checkpoint = new double[20];
        for (var sample = 0; sample < full.Length; sample++)
        {
            TState baseline;
            TState restored;
            if (sample % 2 == 0)
            {
                baseline = await ReplayFull(sample);
                restored = await Restore(sample);
            }
            else
            {
                restored = await Restore(sample);
                baseline = await ReplayFull(sample);
            }
            Assert.Equal(130, restored.CommittedStreamVersion);
            Assert.Equal(130, restored.Operation.Revision);
            Compare(baseline, restored);
        }

        var stream = await source.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
        var versions = new List<long>();
        await source.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(
            stream, [typeof(DatabaseOperationStateSnapshottedEvent)], page => versions.AddRange(page.Select(row => row.StreamVersion)));
        Assert.Equal(new long[] { 1, 129, 130 }, versions);
        Array.Sort(full);
        Array.Sort(checkpoint);
        output.WriteLine($"{typeof(TState).Name}: 131 -> 4 rows including legacy, median " +
            $"{(full[9] + full[10]) / 2:F2} -> {(checkpoint[9] + checkpoint[10]) / 2:F2} ms, " +
            $"p95 {full[18]:F2} -> {checkpoint[18]:F2} ms.");

        await source.SaveEventsAsync(legacy, Guid.NewGuid(), new DomainEventCollection([Fact(131)]), 1, CancellationToken.None);
        var afterLegacyChange = await repository.LoadStateAsync(command);
        Assert.Equal(131, afterLegacyChange.Operation.Revision);
        Assert.Equal(130, afterLegacyChange.CommittedStreamVersion);
        Assert.Equal(2, afterLegacyChange.LegacyStreamVersion);

        DatabaseSourceEnvelope Envelope(int sequence) => new()
        {
            OperationId = id, ProtectionSetId = new("core"), Source = BackupSource.LocalWorkstation,
            OperationKind = DatabaseRecoveryOperationKind.Restore, Phase = DatabaseRecoveryPhase.Validating,
            ProducingHostId = new("snapshot-host"), SourceEventId = Guid.NewGuid(),
            SourceRevisionOrSequence = sequence, ObservedUtc = DateTimeOffset.UnixEpoch
        };

        IEvent Fact(int sequence) => new DatabaseOperationProgressRecordedEvent
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = id, Source = Envelope(sequence),
            ProgressPercent = Math.Min(sequence, 100), RestorePointId = new("snapshot-point")
        };

        void Compare(TState baseline, TState restored)
        {
            var expected = baseline.CaptureSnapshot(command);
            var actual = restored.CaptureSnapshot(command);
            Assert.Equivalent(expected.Operation, actual.Operation);
            Assert.Equivalent(expected.ProcessedServiceEvents, actual.ProcessedServiceEvents);
            Assert.Equivalent(expected.Service, actual.Service);
            Assert.Equivalent(expected.Restore, actual.Restore);
            Assert.Equivalent(expected.Policy, actual.Policy);
            Assert.Equivalent(expected.BackupSet, actual.BackupSet);
            Assert.Equivalent(expected.Retention, actual.Retention);
        }

        async Task<TState> ReplayFull(int sample)
        {
            var started = Stopwatch.GetTimestamp();
            var baseline = new TState { Id = command.Subject.ThreadId };
            var legacyId = await source.GetEventStreamIdAsync(legacy, CancellationToken.None);
            await source.MapReduceActorEventStreamFromCreationAsync<TState>(legacyId, page =>
                baseline.ReplayEvents(page.Select(row =>
                {
                    baseline.LegacyStreamVersion = row.StreamVersion;
                    return row.ToDomainEvent();
                })));
            var current = await source.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            await source.MapReduceActorEventStreamFromCreationAsync<TState>(current, baseline.ReplayEvents);
            full[sample] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            return baseline;
        }

        async Task<TState> Restore(int sample)
        {
            var started = Stopwatch.GetTimestamp();
            var state = await repository.LoadStateAsync(command);
            checkpoint[sample] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            return state;
        }
    }

    private IDbContextFactory Databases()
    {
        var factory = Substitute.For<IDbContextFactory>();
        var database = Substitute.For<ISystemAdminDbContext>();
        database.ProviderName.Returns("System.Data.Postgres");
        database.ConnectionString.Returns(fixture.ActorEventDb.ConnectionString);
        factory.SystemAdminDb.Returns(database);
        return factory;
    }
}
