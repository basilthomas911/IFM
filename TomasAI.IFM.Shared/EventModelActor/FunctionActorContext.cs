using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Default context for a Core NATS Function actor.</summary>
public class FunctionActorContext(IActorSupervisor supervisor, ActorMailboxId actorId)
    : IFunctionActorContext
{
    readonly IActorSupervisor _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
    readonly ActorMailboxId _actorId = actorId;

    public ActorMailboxId ActorId => _actorId;
    public IContainerInstance Container => _supervisor.Container;
}
