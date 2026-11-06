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
        Verify<DatabaseBackupCommandState>(DatabaseBackupCommandActor.Actor, (factory, transactions) =>
            new DatabaseBackupStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseBackupSet_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupSetCommandState>(DatabaseBackupSetCommandActor.Actor, (factory, transactions) =>
            new DatabaseBackupSetStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupSetCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupSetStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseBackupPolicy_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupPolicyCommandState>(DatabaseBackupPolicyCommandActor.Actor, (factory, transactions) =>
            new DatabaseBackupPolicyStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupPolicyCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupPolicyStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseRecovery_restores_operation_and_concern_state() =>
        Verify<DatabaseRecoveryCommandState>(DatabaseRecoveryCommandActor.Actor, (factory, transactions) =>
            new DatabaseRecoveryStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseRecoveryCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseRecoveryStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseBackupRetention_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupRetentionCommandState>(DatabaseBackupRetentionCommandActor.Actor, (factory, transactions) =>
            new DatabaseBackupRetentionStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupRetentionCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupRetentionStateRepository>.Instance, Databases(), transactions));

    [Fact]
    public Task DatabaseBackupService_restores_operation_and_concern_state() =>
        Verify<DatabaseBackupServiceCommandState>(DatabaseBackupServiceCommandActor.Actor, (factory, transactions) =>
            new DatabaseBackupServiceStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
                Substitute.For<IEventProjector<DatabaseBackupServiceCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
                NullLogger<DatabaseBackupServiceStateRepository>.Instance, Databases(), transactions));

    private async Task Verify<TState>(string actor, Func<IEventSourceActorStateFactory, IPostgresEventTransaction,
        IEventSourceActorStateRepository<TState>> create) where TState : DatabaseOperationCommandState<TState>, new()
    {
        var id = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var legacy = $"Command.{DatabaseBackupCommandActor.Actor}.{id.Format()}";
        var legacyCount = actor == DatabaseBackupCommandActor.Actor ? 0 : 1;
        var command = new RecordDatabaseOperationProgressCommand
        {
            CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, actor, "RecordProgress", id.Format()),
            Source = Envelope(128 + legacyCount), ProgressPercent = 75
        };
        var source = fixture.ActorEventDb;
        var transactions = new PostgresEventTransaction(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, source.ConnectionString, "System.Data.Postgres"));
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<TState>().Returns(_ => new TState());
        var repository = create(factory, transactions);
        if (legacyCount > 0)
            await source.SaveEventsAsync(legacy, Guid.NewGuid(), new DomainEventCollection([Fact(1)]), 0, CancellationToken.None);
        var legacyOnly = await repository.LoadStateAsync(command);
        Assert.Equal(legacyCount, legacyOnly.Operation.Revision);
        Assert.Equal(legacyCount, legacyOnly.LegacyStreamVersion);
        Assert.Equal(0, legacyOnly.CommittedStreamVersion);
        await source.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection(
            Enumerable.Range(legacyCount + 1, 127).Select(i => Fact(i))), 0, CancellationToken.None);
        var pending = await repository.LoadStateAsync(command);
        Assert.Equal(127 + legacyCount, pending.Operation.Revision);
        Assert.Equal(127, pending.CommittedStreamVersion);
        Assert.True(pending.Update(Fact(128 + legacyCount), command));
        await repository.SaveStateAsync(Substitute.For<ICommandActorContext>(), pending, command);
        Assert.Equal(129, pending.CommittedStreamVersion);

        await source.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([Fact(129 + legacyCount)]),
            129, CancellationToken.None);
        var full = new double[20];
        var previousCheckpoint = new double[20];
        var checkpoint = new double[20];
        for (var sample = 0; sample < full.Length; sample++)
        {
            TState baseline;
            TState restored;
            if (sample % 2 == 0)
            {
                baseline = await ReplayFull(sample);
                await ReplayPreviousCheckpoint(sample);
                restored = await Restore(sample);
            }
            else
            {
                restored = await Restore(sample);
                await ReplayPreviousCheckpoint(sample);
                baseline = await ReplayFull(sample);
            }
            Assert.Equal(130, restored.CommittedStreamVersion);
            Assert.Equal(129 + legacyCount, restored.Operation.Revision);
            Compare(baseline, restored);
        }

        var versions = new List<long>();
        await source.ReadConcernCheckpointWindowAsync(command.StreamId, legacy, typeof(DatabaseOperationStateSnapshottedEvent), true, page =>
        {
            foreach (var row in page)
            {
                Assert.False(row.IsLegacy);
                Assert.Equal(legacyCount, row.LegacyHead);
                Assert.Equal(130, row.ConcernHead);
                Assert.Equal(129, row.CheckpointVersion);
                versions.Add(Assert.IsType<TomasAI.IFM.Shared.EventSourcing.ViewModels.EventStreamReadModel>(row.Event).StreamVersion);
            }
        }, CancellationToken.None);
        Assert.Equal(new long[] { 1, 129, 130 }, versions);
        Array.Sort(full);
        Array.Sort(previousCheckpoint);
        Array.Sort(checkpoint);
        var previousRows = legacyCount == 0 ? 130 : 4;
        output.WriteLine($"{typeof(TState).Name}: full replay / previous loader / paired checkpoint, {130 + legacyCount} / {previousRows} / 3 rows, " +
            $"median {Median(full):F2} / {Median(previousCheckpoint):F2} / {Median(checkpoint):F2} ms, " +
            $"p95 {full[18]:F2} / {previousCheckpoint[18]:F2} / {checkpoint[18]:F2} ms (stream resolution included).");

        var recovered = await repository.LoadStateAsync(command);
        if (legacyCount > 0)
        {
            await source.SaveEventsAsync(legacy, Guid.NewGuid(), new DomainEventCollection([Fact(131)]), 1, CancellationToken.None);
            recovered = await repository.LoadStateAsync(command);
            Assert.Equal(131, recovered.Operation.Revision);
            Assert.Equal(130, recovered.CommittedStreamVersion);
            Assert.Equal(2, recovered.LegacyStreamVersion);
            Compare(await ReplayFull(0), recovered);
        }

        var incompatible = recovered.CaptureSnapshot(command) with { Concern = "WrongConcern" };
        await source.SaveEventsAsync(command.StreamId, Guid.NewGuid(), new DomainEventCollection([incompatible]),
            130, CancellationToken.None);
        recovered = await repository.LoadStateAsync(command);
        Assert.Equal(legacyCount > 0 ? 131 : 129, recovered.Operation.Revision);
        Assert.Equal(131, recovered.CommittedStreamVersion);
        Compare(await ReplayFull(0), recovered);

        await transactions.ExecuteAsync((database, token) => database.ExecuteAsync("""
            UPDATE public.event_log e SET EventPayload=decode('00','hex')
            FROM public.event_stream_id s
            WHERE s.EventStreamId=e.EventStreamId AND s.EventStream=$1 AND e.StreamVersion=131;
            """, [command.StreamId], token));
        var afterCorruption = await repository.LoadStateAsync(command);
        Assert.Equal(131, afterCorruption.CommittedStreamVersion);
        Compare(recovered, afterCorruption);
        recovered = afterCorruption;

        var next = command with { CommandId = Guid.NewGuid(), Source = Envelope(132) };
        Assert.True(recovered.Update(Fact(132), next));
        await repository.SaveStateAsync(Substitute.For<ICommandActorContext>(), recovered, next);
        Assert.Equal(132, recovered.CommittedStreamVersion);
        var appended = await repository.LoadStateAsync(next);
        Assert.Equal(legacyCount > 0 ? 132 : 130, appended.Operation.Revision);
        Assert.Equal(132, appended.CommittedStreamVersion);
        Compare(recovered, appended);

        await transactions.ExecuteAsync((database, token) => database.ExecuteAsync("""
            UPDATE public.event_stream_id SET CurrentVersion=CurrentVersion+1 WHERE EventStream=$1;
            """, [command.StreamId], token));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await repository.LoadStateAsync(next));

        await transactions.ExecuteAsync(async (database, token) =>
        {
            await database.ExecuteAsync("""
                UPDATE public.event_stream_id SET CurrentVersion=CurrentVersion-1 WHERE EventStream=$1;
                """, [command.StreamId], token);
            return await database.ExecuteAsync("""
                UPDATE public.event_log e SET EventPayload=decode('00','hex')
                FROM public.event_stream_id s
                WHERE s.EventStreamId=e.EventStreamId AND s.EventStream=$1 AND e.StreamVersion=132;
                """, [command.StreamId], token);
        });
        await Assert.ThrowsAsync<InvalidDataException>(async () => await repository.LoadStateAsync(next));

        static double Median(double[] samples) => (samples[9] + samples[10]) / 2;

        DatabaseSourceEnvelope Envelope(int sequence) => new()
        {
            OperationId = id, ProtectionSetId = new("core"), Source = BackupSource.LocalWorkstation,
            OperationKind = DatabaseRecoveryOperationKind.Restore, Phase = DatabaseRecoveryPhase.Validating,
            ProducingHostId = new("snapshot-host"), SourceEventId = Guid.NewGuid(),
            SourceRevisionOrSequence = sequence, ObservedUtc = DateTimeOffset.UnixEpoch
        };

        IEvent Fact(int sequence)
        {
            if (sequence == 64 && typeof(TState) == typeof(DatabaseBackupSetCommandState))
                return new DatabaseBackupSetCheckpointRecordedEvent
                {
                    Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = id,
                    Source = Envelope(sequence) with { BackupSetId = new(Guid.NewGuid()) }
                };
            return new DatabaseOperationProgressRecordedEvent
            {
                Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = id, Source = Envelope(sequence),
                ProgressPercent = Math.Min(sequence, 100), RestorePointId = new("snapshot-point"),
                Policy = sequence == 64 && typeof(TState) == typeof(DatabaseBackupPolicyCommandState)
                    ? new([BackupSource.LocalWorkstation], [new("core")], new(TimeSpan.FromHours(1), TimeSpan.FromHours(2)),
                        new(7, 4, 12), new([DatabaseVerificationLevel.Checksum], TimeSpan.FromDays(1))) : null,
                RetentionPlanId = sequence == 64 && typeof(TState) == typeof(DatabaseBackupRetentionCommandState)
                    ? new(Guid.NewGuid()) : null,
                RetentionPlanRevision = sequence == 64 ? 2 : 0
            };
        }

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
            await ReplayLegacy(baseline);
            var current = await source.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            await source.MapReduceActorEventStreamFromCreationAsync<TState>(current, page =>
                baseline.ReplayEvents(page.Select(row => row.ToDomainEvent()).Where(fact => fact is not DatabaseOperationStateSnapshottedEvent)));
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

        async Task ReplayPreviousCheckpoint(int sample)
        {
            var started = Stopwatch.GetTimestamp();
            var baseline = new TState { Id = command.Subject.ThreadId };
            await ReplayLegacy(baseline);
            var current = await source.GetEventStreamIdAsync(command.StreamId, CancellationToken.None);
            if (legacyCount == 0)
                await source.MapReduceActorEventStreamFromCreationAsync<TState>(current, page =>
                    baseline.ReplayEvents(page.Select(row => row.ToDomainEvent()).Where(fact => fact is not DatabaseOperationStateSnapshottedEvent)));
            else
                await source.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<TState>(current,
                    [typeof(DatabaseOperationStateSnapshottedEvent)], baseline.ReplayEvents);
            previousCheckpoint[sample] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            Assert.Equal(129 + legacyCount, baseline.Operation.Revision);
        }

        async Task ReplayLegacy(TState baseline)
        {
            if (legacyCount == 0) return;
            var legacyId = await source.GetEventStreamIdAsync(legacy, CancellationToken.None);
            await source.MapReduceActorEventStreamFromCreationAsync<TState>(legacyId, page =>
                baseline.ReplayEvents(page.Select(row =>
                {
                    baseline.LegacyStreamVersion = row.StreamVersion;
                    return row.ToDomainEvent();
                }).Where(fact => fact is not DatabaseOperationStateSnapshottedEvent)));
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
