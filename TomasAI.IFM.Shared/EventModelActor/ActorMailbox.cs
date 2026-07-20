using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Represents a mailbox that manages message processing for an actor within the actor model, providing thread-safe
/// queuing and delivery of messages.
/// </summary>
/// <remarks>The ActorMailbox encapsulates thread queues to ensure that messages are processed in a thread-safe
/// manner. It is typically used internally by actor frameworks to coordinate message delivery and actor supervision.
/// Instances of this class are intended to be unique per actor.</remarks>
/// <param name="supervisor">The supervisor responsible for managing the lifecycle and supervision of the actor associated with this mailbox.</param>
/// <param name="id">The unique identifier for the actor mailbox, used to distinguish it from other mailboxes.</param>
public class ActorMailbox(IActorSupervisor supervisor, ActorMailboxId id) 
    : IActorMailbox
{
    readonly IActorThreadQueues _threadQueues = new ActorThreadQueues(supervisor);

    public ActorMailboxId Id 
        => IsArgumentNull.Set(id);

    public IActorThreadQueues ThreadQueues 
        => _threadQueues;
}
