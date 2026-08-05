using TomasAI.IFM.Domain.Application.Actor;
using TomasAI.IFM.Domain.Application.Actor.Command.State;
using TomasAI.IFM.Domain.Application.Actor.Event.Actor;
using TomasAI.IFM.Domain.Application.Shared.Events;

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
}
