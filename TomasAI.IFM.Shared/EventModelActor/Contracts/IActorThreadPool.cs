namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Manages the collection of actor threads and the lifecycle of actors within the actor system.
/// </summary>
/// <remarks>
/// An implementing type is responsible for creating, tracking and disposing actor threads, routing messages
/// to the correct thread, and exposing runtime state information about managed threads.
/// </remarks>
public interface IActorThreadPool
{
    /// <summary>
    /// Initializes the actor thread pool with the specified number of threads.
    /// </summary>
    /// <param name="threadCount">The number of threads to allocate for the actor thread pool. Must be greater than zero.</param>
    /// <returns>An instance of <see cref="IActorThreadPool"/> configured with the specified number of threads.</returns>
    IActorThreadPool Initialize(int threadCount);

    /// <summary>
    /// Retrieves the thread associated with the specified actor.
    /// </summary>
    /// <param name="actor">The actor for which to retrieve the associated thread. Cannot be <see langword="null"/>.</param>
    /// <returns>The <see cref="IActorThread"/> associated with the specified actor.</returns>
    IActorThread GetThread(ActorThreadId threadId);

    /// <summary>
    /// Asynchronously retrieves the actor thread associated with the specified thread identifier.
    /// </summary>
    /// <remarks>This method is intended for use in asynchronous contexts. Monitor the cancellation token to
    /// handle cancellation scenarios appropriately.</remarks>
    /// <param name="threadId">The identifier of the actor thread to retrieve. Must be a valid thread identifier.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task that represents the asynchronous operation. The result contains the requested actor thread, or null
    /// if the thread is not found.</returns>
    ValueTask<IActorThread> GetThreadAsync(ActorThreadId threadId, CancellationToken ct);

    /// <summary>
    /// Releases the specified thread, allowing it to be reused by the system.
    /// </summary>
    /// <remarks>Once a thread is released, it is no longer associated with its previous task and can be
    /// reassigned.  Ensure that the thread is no longer in use before calling this method to avoid unexpected
    /// behavior.</remarks>
    /// <param name="threadId">The identifier of the thread to release. This must be a valid thread ID that is currently in use.</param>
    void ReleaseThread(ActorThreadId threadId);

    /// <summary>
    /// Gets the number of actor threads currently managed by the pool.
    /// </summary>
    int Count { get; }
}
