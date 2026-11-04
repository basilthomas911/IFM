using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskRunStateRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Load_restores_requested_snapshot_and_lifecycle_events_with_committed_version(bool cancellable)
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var command = new RecordScheduledTaskRunStartedCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, RecordScheduledTaskRunStartedCommand.Actor, RecordScheduledTaskRunStartedCommand.Verb, id.Format()) };
        var initial = new ScheduledTaskRun { Id = id, Revision = 1, Status = ScheduledTaskRunStatus.Requested };
        var changed = initial with { Revision = 2, Status = ScheduledTaskRunStatus.Admitted };
        var running = changed with { Revision = 3, Status = ScheduledTaskRunStatus.Running, ProcessId = 1234 };
        IEvent[] events = [
            new ScheduledTaskRunRequestedEvent { EntityId = id, ScheduledTaskRun = initial },
            new ScheduledTaskRunAdmissionRecordedEvent { EntityId = id, ScheduledTaskRun = changed },
            new ScheduledTaskRunStartedEvent { EntityId = id, ScheduledTaskRun = running }
        ];
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskRunCommandState>().Returns(new ScheduledTaskRunCommandState());
        var store = Substitute.For<IEventSourceActorDbContext>();
        store.ProviderName.Returns("System.Data.Postgres");
        using var deadline = new CancellationTokenSource();
        var token = cancellable ? deadline.Token : CancellationToken.None;
        store.GetEventStreamIdAsync(command.StreamId, token).Returns(91L);
        store.GetCommittedStreamVersionAsync(91, token).Returns(3L);
        store.MapReduceActorEventStreamFromCreationAsync<ScheduledTaskRunCommandState>(91,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token).Returns(call =>
        {
            var rows = events.Select((source, index) => new EventStreamReadModel
            {
                EventVersion = index + 1, StreamVersion = index + 1,
                EventTypeName = source.GetType().AssemblyQualifiedName!,
                EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(source)
            });
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(rows);
            return ValueTask.CompletedTask;
        });
        var repository = new ScheduledTaskRunStateRepository(factory, store, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskRunCommandActor>>(), NullLogger<ScheduledTaskRunStateRepository>.Instance);

        var loaded = cancellable ? await repository.LoadStateAsync(command, token) : await repository.LoadStateAsync(command);

        Assert.Equal(running, loaded.ScheduledTaskRun);
        Assert.Equal(command.Subject.ThreadId, loaded.Id);
        Assert.Equal(3, loaded.CommittedStreamVersion);
        Assert.Empty(loaded.Events); Assert.False(loaded.Updated);
        await store.Received(1).MapReduceActorEventStreamFromCreationAsync<ScheduledTaskRunCommandState>(91,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token);
        await store.DidNotReceiveWithAnyArgs().MapReduceActorEventStreamAsync<ScheduledTaskRunCommandState>(default, default!, default);
    }
}
