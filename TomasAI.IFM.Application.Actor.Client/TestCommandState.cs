using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Actor.Client;

public class TestCommandState : IActorState<TestCommandState>
{
    public ActorThreadId Id { get; set; } = new ActorThreadId(ActorType.Command, "Test", new TestId(DateOnly.FromDateTime(DateTime.UtcNow)).Format());
}
