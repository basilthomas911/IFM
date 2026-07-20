using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents a mailbox for an actor, providing access to its unique identifier and message queue.
/// </summary>
/// <remarks>This interface defines the contract for an actor's mailbox, which includes a unique identifier and a
/// queue for managing messages sent to the actor. Implementations of this interface are responsible for ensuring
/// thread-safe access to the mailbox and its queue.</remarks>
public interface IActorMailbox
{
    IActorThreadQueues ThreadQueues { get; }
}
