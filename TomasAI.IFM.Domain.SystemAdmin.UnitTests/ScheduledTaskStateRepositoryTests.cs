using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskStateRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Load_restores_creation_snapshot_and_later_changes_with_committed_version(bool cancellable)
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var command = new EnableScheduledTaskCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, EnableScheduledTaskCommand.Actor, EnableScheduledTaskCommand.Verb, id.Format()) };
        var initial = new ScheduledTaskDefinition { Id = id, Revision = 1 };
        var changed = initial with { Revision = 2, Enabled = true };
        var admitted = changed with { Revision = 3, ActiveRunId = Guid.NewGuid() };
        IEvent[] events = [
            new ScheduledTaskCreatedEvent { EntityId = id, ScheduledTaskDefinition = initial },
            new ScheduledTaskEnabledEvent { EntityId = id, ScheduledTaskDefinition = changed },
            new ScheduledTaskRunAdmittedEvent { EntityId = id, ScheduledTaskDefinition = admitted }
        ];
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskCommandState>().Returns(new ScheduledTaskCommandState());
        var store = Substitute.For<IEventSourceActorDbContext>();
        store.ProviderName.Returns("System.Data.Postgres");
        using var deadline = new CancellationTokenSource();
        var token = cancellable ? deadline.Token : CancellationToken.None;
        store.GetEventStreamIdAsync(command.StreamId, token).Returns(91L);
        store.GetCommittedStreamVersionAsync(91, token).Returns(3L);
        store.MapReduceActorEventStreamAsync<ScheduledTaskCommandState, ScheduledTaskCreatedEvent>(91,
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
        var repository = new ScheduledTaskStateRepository(factory, store, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskCommandActor>>(), NullLogger<ScheduledTaskStateRepository>.Instance);

        var loaded = cancellable ? await repository.LoadStateAsync(command, token) : await repository.LoadStateAsync(command);

        Assert.Equal(admitted, loaded.ScheduledTaskDefinition);
        Assert.Equal(command.Subject.ThreadId, loaded.Id);
        Assert.Equal(3, loaded.CommittedStreamVersion);
        Assert.Empty(loaded.Events); Assert.False(loaded.Updated);
        await store.Received(1).MapReduceActorEventStreamAsync<ScheduledTaskCommandState, ScheduledTaskCreatedEvent>(91,
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token);
        await store.DidNotReceiveWithAnyArgs().MapReduceActorEventStreamAsync<ScheduledTaskCommandState>(default, default!, default);
    }
}
