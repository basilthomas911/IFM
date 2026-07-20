using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts
{
    /// <summary>
    /// Manages actor threads and the lifecycle of actors within the actor system.
    /// </summary>
    public interface IActorThreadPool
    {
        /// <summary>
        /// Adds the specified actor to the pool and associates it with an actor thread.
        /// </summary>
        /// <param name="actor">The actor to add to the pool.</param>
        /// <returns>True if the actor was successfully added; otherwise false.</returns>
        bool AddActor(IActor actor);

        /// <summary>
        /// Removes the actor identified by the specified mailbox identifier from the pool and releases any associated resources.
        /// </summary>
        /// <param name="mailboxId">The mailbox identifier of the actor to remove.</param>
        /// <returns>True if the actor was found and removed; otherwise false.</returns>
        bool RemoveActor(ActorMailboxId mailboxId);

        /// <summary>
        /// Gets the actor thread associated with the given actor subject.
        /// </summary>
        /// <param name="subject">The subject that identifies the actor.</param>
        /// <returns>The <see cref="IActorThread"/> associated with the subject, or null if none exists.</returns>
        IActorThread GetActorThread(ActorSubject subject);

        /// <summary>
        /// Determines whether an actor with the specified mailbox identifier exists in the pool.
        /// </summary>
        /// <param name="mailboxId">The mailbox identifier to check.</param>
        /// <returns>True if the actor exists in the pool; otherwise false.</returns>
        bool Exists(ActorMailboxId mailboxId);

        /// <summary>
        /// Determines whether the actor identified by the specified mailbox identifier is currently running.
        /// </summary>
        /// <param name="mailboxId">The mailbox identifier of the actor to check.</param>
        /// <returns>True if the actor is running; otherwise false.</returns>
        bool IsRunning(ActorMailboxId mailboxId);

        /// <summary>
        /// Determines whether the actor identified by the specified mailbox identifier has been started.
        /// </summary>
        /// <param name="mailboxId">The mailbox identifier of the actor to check.</param>
        /// <returns>True if the actor has been started; otherwise false.</returns>
        bool IsStarted(ActorMailboxId mailboxId);

        /// <summary>
        /// Determines whether the actor identified by the specified mailbox identifier has been stopped.
        /// </summary>
        /// <param name="mailboxId">The mailbox identifier of the actor to check.</param>
        /// <returns>True if the actor has been stopped; otherwise false.</returns>
        bool IsStopped(ActorMailboxId mailboxId);

        /// <summary>
        /// Gets the number of actors currently managed by the pool.
        /// </summary>
        int Count { get; }
    }
}
