using TomasAI.IFM.Domain.Application.Actor;
using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Domain.Application.Actor.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Application.Actor.UnitTests;

public sealed class ApplicationActorTests
{
    [Fact]
    public void AssemblyMarkerReturnsTheApplicationActorAssembly()
        => Assert.Equal(typeof(ApplicationCommandState).Assembly, ApplicationActorAssembly.Current);

    [Fact]
    public void EventActorUsesTheApplicationEventMailbox()
        => Assert.Equal(ApplicationStartupEvent.Actor, ApplicationEventActor.Actor);

    [Fact]
    public void CommandStateAppliesBothLifecycleEvents()
    {
        var state = new ApplicationCommandState();

        Assert.True(state.Update(new ApplicationStartupEvent()));
        Assert.True(state.Update(new ApplicationShutdownEvent()));
        Assert.True(state.Updated);
        Assert.Collection(
            state.Events,
            @event => Assert.IsType<ApplicationStartupEvent>(@event),
            @event => Assert.IsType<ApplicationShutdownEvent>(@event));
    }

    [Fact]
    public void ShutdownEventConvertsToApplicationCompletionEvent()
    {
        var entityId = new ApplicationEntityId(new DateOnly(2026, 8, 6));
        var shutdown = new ApplicationShutdownEvent
        {
            Subject = new ActorSubject(
                ActorType.Event,
                ApplicationShutdownEvent.Actor,
                ApplicationShutdownEvent.Verb,
                entityId.Format()),
            EntityId = entityId,
            Id = Guid.NewGuid(),
            EventId = 42,
            CommandId = Guid.NewGuid(),
            AggregateId = "application-20260806",
            EventSource = "ApplicationCommandActor",
            ReceivedOn = DateTime.UtcNow,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "test"
        };

        var completed = shutdown.ToCompleteEvent<ApplicationShutdownCompleteEvent, ApplicationEntityId>();

        var typedCompletion = Assert.IsType<ApplicationShutdownCompleteEvent>(completed);
        Assert.Equal(shutdown.EntityId, typedCompletion.EntityId);
        Assert.Equal(shutdown.Id, typedCompletion.Id);
        Assert.Equal(shutdown.EventId, typedCompletion.EventId);
        Assert.Equal(shutdown.CommandId, typedCompletion.CommandId);
        Assert.Equal(shutdown.AggregateId, typedCompletion.AggregateId);
    }
}
