using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Persistence;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;
public sealed class DatabaseBackupConcernRepositoryTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Recovery_replays_legacy_history_then_its_own_stream_without_inheriting_the_legacy_append_version(bool hasOwnHistory, bool cancellable)
    {
        var id = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var point = new DatabaseRestorePointId("isolated-point");
        var source = new DatabaseSourceEnvelope { OperationId = id, Source = BackupSource.LocalWorkstation,
            OperationKind = DatabaseRecoveryOperationKind.Restore, ProtectionSetId = new("core"),
            Phase = DatabaseRecoveryPhase.Requested, SourceEventId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow };
        DatabaseSourceEnvelope Observation(long sequence, DatabaseRecoveryPhase phase) => source with
        { SourceEventId = Guid.NewGuid(), ProducingHostId = new("test-host"), SourceRevisionOrSequence = sequence, Phase = phase };
        DatabaseBackupEventContract[] legacy = [
            new DatabaseRestoreRequestedDomainEvent { EntityId = id, Id = source.SourceEventId, Source = source, RestorePointId = point },
            new DatabaseOperationAdmissionRecordedEvent { EntityId = id, Id = Guid.NewGuid(), Source = Observation(1, DatabaseRecoveryPhase.Admitted) },
            new DatabaseOperationStartedEvent { EntityId = id, Id = Guid.NewGuid(), Source = Observation(2, DatabaseRecoveryPhase.Started) }
        ];
        var progress = new DatabaseOperationProgressRecordedEvent { EntityId = id, Id = Guid.NewGuid(),
            Source = Observation(3, DatabaseRecoveryPhase.Validating), ProgressPercent = 90 };
        var command = new RecordDatabaseOperationProgressCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, DatabaseRecoveryCommandActor.Actor, "RecordProgress", id.Format()),
            Source = Observation(hasOwnHistory ? 4 : 3, DatabaseRecoveryPhase.Validating), ProgressPercent = 95 };
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<DatabaseRecoveryCommandState>().Returns(_ => new DatabaseRecoveryCommandState());
        var store = Substitute.For<IEventSourceActorDbContext>();
        store.ProviderName.Returns("System.Data.Postgres");
        using var deadline = new CancellationTokenSource();
        var token = cancellable ? deadline.Token : CancellationToken.None;
        store.GetEventStreamIdAsync($"Command.DatabaseBackupCommand.{id.Format()}", token).Returns(10L);
        store.GetEventStreamIdAsync(command.StreamId, token).Returns(20L);
        store.GetCommittedStreamVersionAsync(20, token).Returns(hasOwnHistory ? 1L : 0L);
        store.MapReduceActorEventStreamFromCreationAsync<DatabaseRecoveryCommandState>(Arg.Any<long>(),
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token).Returns(call =>
        {
            var events = call.Arg<long>() == 10 ? legacy : hasOwnHistory ? new DatabaseBackupEventContract[] { progress } : [];
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(events.Select((e, index) => new EventStreamReadModel
            {
                EventVersion = index + 1, StreamVersion = index + 1, EventTypeName = e.GetType().AssemblyQualifiedName!,
                EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(e)
            }));
            return ValueTask.CompletedTask;
        });
        store.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<DatabaseRecoveryCommandState>(20,
            Arg.Any<IReadOnlyList<Type>>(), Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token).Returns(call =>
        {
            var events = hasOwnHistory ? new[] { progress } : [];
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(events.Select((e, index) => new EventStreamReadModel
            {
                EventVersion = index + 1, StreamVersion = index + 1, EventTypeName = e.GetType().AssemblyQualifiedName!,
                EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(e)
            }));
            return ValueTask.CompletedTask;
        });
        var repository = new DatabaseRecoveryStateRepository(factory, store, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<DatabaseRecoveryCommandActor>>(), Substitute.For<IDatabaseBackupExecutionOutbox>(),
            NullLogger<DatabaseRecoveryStateRepository>.Instance);
        var loaded = await repository.LoadStateAsync(command, token);
        Assert.Equal(point, loaded.Restore.RestorePointId);
        Assert.Equal(hasOwnHistory ? 4 : 3, loaded.Operation.Revision);
        Assert.Equal(hasOwnHistory ? 1 : 0, loaded.CommittedStreamVersion);
        Assert.Empty(loaded.Events);
        Assert.False((command with { ExpectedStateRevision = 99 }).Execute(loaded).Success);
        Assert.Empty(loaded.Events);
        Assert.True(command.Execute(loaded).Success);
        Assert.Single(loaded.Events);
        Assert.Equal(command.Source.SourceRevisionOrSequence, loaded.Operation.LastServiceSequence);
        await store.DidNotReceive().GetCommittedStreamVersionAsync(20, token);
        await store.DidNotReceive().GetCommittedStreamVersionAsync(10, token);
    }
}
