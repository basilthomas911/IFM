using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Actor;

public class TestEventState : IActorState<TestEventState>
{
    ActorThreadId _threadId = new ActorThreadId(ActorType.Query, "Test", new TestId(DateOnly.FromDateTime(DateTime.UtcNow)).Format());
    public ActorThreadId Id { get => _threadId; set => _threadId = value; }
}
