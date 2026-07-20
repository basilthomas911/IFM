using NATS.Client.Core;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents the execution thread (or execution context) for a single actor.
/// </summary>
public interface IActorThread
{
    /// <summary>
    /// Gets the unique identifier of the actor thread.
    /// </summary>
    ActorThreadId Id { get; set; }

    /// <summary>
    /// Gets the current runtime state of the actor thread.
    /// </summary>
    ActorThreadState State { get; }

    /// <summary>
    /// Posts a message to the actor thread for processing.
    /// </summary>
    /// <param name="message">The message to post.</param>
    /// <returns>True if the message was accepted for processing; otherwise false.</returns>
    bool Post(in NatsMsg<byte[]> message);

    /// <summary>
    /// Asynchronously writes the specified message to the actor thread queue for processing.
    /// </summary>
    /// <remarks>This method is intended for use in actor-based systems to ensure thread-safe queuing of
    /// messages for later processing. The operation is asynchronous and may be cancelled using the provided
    /// token.</remarks>
    /// <param name="message">The message to enqueue, represented as a <see cref="NatsMsg{T}"/> containing a byte array. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation. The task completes when the message has
    /// been successfully enqueued.</returns>
    ValueTask WriteToActorThreadQueueAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously writes the specified message to the actor thread queue using a pre-parsed subject
    /// to avoid redundant subject parsing.
    /// </summary>
    /// <param name="message">The message to enqueue.</param>
    /// <param name="subject">The pre-parsed actor subject.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask WriteToActorThreadQueueAsync(NatsMsg<byte[]> message, ActorSubject subject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the operation and indicates whether it was initiated successfully.
    /// </summary>
    /// <returns>true if the operation started successfully; otherwise, false.</returns>
    bool Start();

    /// <summary>
    /// Sets the availability status of the message, indicating that it can be processed.
    /// </summary>
    /// <remarks>This method should be called when a new message is available for processing. It may trigger
    /// any associated event handlers or notifications to inform listeners of the new message status.</remarks>
    /// <param name="threadId">The identifier of the actor thread that has a new message available.</param>
    void SignalMessageAvailable(ActorThreadId threadId);

    /// <summary>
    /// Starts the specified actor on the given thread.
    /// </summary>
    /// <remarks>Ensure that the actor is not already running on another thread before calling this
    /// method.</remarks>
    /// <param name="actor">The actor to be started. This parameter cannot be null.</param>
    /// <param name="threadId">The identifier of the thread on which the actor will run. Must be a valid thread ID.</param>
    /// <returns>true if the actor was successfully started; otherwise, false.</returns>
    bool Start(IActor actor, ActorThreadId threadId);

    /// <summary>
    /// Stops the actor thread, transitioning it to a stopped state and releasing resources as appropriate.
    /// </summary>
    /// <returns>True if the thread was stopped successfully; otherwise false.</returns>
    bool Stop();

    /// <summary>
    /// Gets a value indicating whether the actor thread is currently running and able to process messages.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets a value indicating whether the actor thread has been started.
    /// </summary>
    bool IsStarted { get; }

    /// <summary>
    /// Gets a value indicating whether the actor thread has been stopped.
    /// </summary>
    bool IsStopped { get; }

    /// <summary>
    /// Gets a value indicating whether the actor thread is in a faulted state.
    /// </summary>
    bool IsFaulted { get; }

    /// <summary>
    /// Gets a value indicating whether the operation has exceeded its allotted time and timed out.
    /// </summary>
    bool IsTimedOut { get; }

    
}
