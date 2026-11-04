using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.Actor;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;

public sealed class ScheduledTaskCatalogStateRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Load_restores_creation_then_capability_and_project_changes(bool cancellable)
    {
        var id = new ScheduledTaskId(Guid.NewGuid());
        var command = new RegisterScheduledTaskProjectCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, RegisterScheduledTaskProjectCommand.Actor, RegisterScheduledTaskProjectCommand.Verb, id.Format()) };
        var initial = new ScheduledTaskCatalog { Id = id, Revision = 12, Projects = [new() { TaskKey = "existing" }] };
        var changed = initial with { Revision = 13, HostCapability = new() { Ready = true, Generation = 2 } };
        var admitted = changed with { Revision = 14, Projects = [.. initial.Projects, new() { TaskKey = "added" }] };
        IEvent[] events = [
            new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = initial },
            new ScheduledTaskCatalogHostCapabilityRecordedEvent { EntityId = id, ScheduledTaskCatalog = changed },
            new ScheduledTaskCatalogProjectRegisteredEvent { EntityId = id, ScheduledTaskCatalog = admitted }
        ];
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ScheduledTaskCatalogCommandState>().Returns(new ScheduledTaskCatalogCommandState());
        var store = Substitute.For<IEventSourceActorDbContext>();
        store.ProviderName.Returns("System.Data.Postgres");
        using var deadline = new CancellationTokenSource();
        var token = cancellable ? deadline.Token : CancellationToken.None;
        store.GetEventStreamIdAsync(command.StreamId, token).Returns(91L);
        store.GetCommittedStreamVersionAsync(91, token).Returns(14L);
        store.MapReduceActorEventStreamFromCreationWithCheckpointsAsync<ScheduledTaskCatalogCommandState>(91, Arg.Any<IReadOnlyList<Type>>(),
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token).Returns(call =>
        {
            var rows = events.Select((source, index) => new EventStreamReadModel
            {
                EventVersion = index + 12, StreamVersion = index + 1,
                EventTypeName = source.GetType().AssemblyQualifiedName!,
                EventData = TomasAI.IFM.Application.EventSourcing.EventLogMessagePackCodec.Shared.Serialize(source)
            });
            call.Arg<Action<IEnumerable<EventStreamReadModel>>>()(rows);
            return ValueTask.CompletedTask;
        });
        var repository = new ScheduledTaskCatalogStateRepository(factory, store, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ScheduledTaskCatalogCommandActor>>(), NullLogger<ScheduledTaskCatalogStateRepository>.Instance);

        var loaded = cancellable ? await repository.LoadStateAsync(command, token) : await repository.LoadStateAsync(command);

        Assert.Equivalent(admitted, loaded.ScheduledTaskCatalog);
        Assert.Equal(command.Subject.ThreadId, loaded.Id);
        Assert.Equal(3, loaded.CommittedStreamVersion);
        Assert.Empty(loaded.Events); Assert.False(loaded.Updated);
        await store.Received(1).MapReduceActorEventStreamFromCreationWithCheckpointsAsync<ScheduledTaskCatalogCommandState>(91, Arg.Any<IReadOnlyList<Type>>(),
            Arg.Any<Action<IEnumerable<EventStreamReadModel>>>(), token);
        await store.DidNotReceiveWithAnyArgs().MapReduceActorEventStreamAsync<ScheduledTaskCatalogCommandState>(default, default!, default);
    }
}
