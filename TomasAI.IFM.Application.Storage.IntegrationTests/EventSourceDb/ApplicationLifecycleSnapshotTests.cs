using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Application.Command.Actor;
using TomasAI.IFM.Domain.Application.Command.State;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class ApplicationLifecycleSnapshotTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Repository_restores_latest_lifecycle_request_and_committed_version(bool shutdownLast)
    {
        var entityId = new ApplicationEntityId(new DateOnly(2026, 11, 4));
        var command = new StartApplicationCommand
        {
            CommandId = Guid.NewGuid(),
            EntityId = entityId,
            Subject = new(ActorType.Command, ApplicationCommandActor.ActorName, StartApplicationCommand.Verb,
                Guid.NewGuid().ToString("N"))
        };
        var startup = new ApplicationStartupEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = entityId };
        var shutdown = new ApplicationShutdownEvent { Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = entityId };
        IEvent[] facts = shutdownLast
            ? [startup, startup with { Id = Guid.NewGuid(), CommandId = Guid.NewGuid() }, shutdown]
            : [shutdown, shutdown with { Id = Guid.NewGuid(), CommandId = Guid.NewGuid() }, startup];
        var stream = command.StreamId;
        await fixture.ActorEventDb.SaveEventsAsync(
            stream, command.CommandId, new DomainEventCollection(facts), 0, CancellationToken.None);

        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<ApplicationCommandState>().Returns(_ => new ApplicationCommandState());
        var repository = new ApplicationStateRepository(factory, fixture.ActorEventDb, Substitute.For<IActorService>(),
            Substitute.For<IEventProjector<ApplicationCommandActor>>(), NullLogger<ApplicationStateRepository>.Instance);

        var restored = await repository.LoadStateAsync(command);

        Assert.Equal(facts[^1].GetType(), restored.LatestRequest?.GetType());
        Assert.Equal(facts[^1].CommandId, restored.LatestRequest?.CommandId);
        Assert.Equal(3, restored.CommittedStreamVersion);
        Assert.Empty(restored.Events);
    }
}
