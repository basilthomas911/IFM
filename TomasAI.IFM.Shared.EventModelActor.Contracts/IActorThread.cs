using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts
{
    /// <summary>
    /// Represents the execution thread (or execution context) for a single actor.
    /// </summary>
    public interface IActorThread
    {
        /// <summary>
        /// Posts a message to the actor thread for processing.
        /// </summary>
        /// <param name="message">The message to post.</param>
        /// <returns>True if the message was accepted for processing; otherwise false.</returns>
        bool Post(IActorMessage message);

        /// <summary>
        /// Starts the actor thread, transitioning it to a running or ready state.
        /// </summary>
        /// <returns>True if the thread was started successfully; otherwise false.</returns>
        bool Start();

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
        /// Gets the current runtime state of the actor thread.
        /// </summary>
        ActorThreadState State { get; }

        /// <summary>
        /// Gets an exception captured when the actor thread transitions to a faulted state.
        /// </summary>
        Exception? Exception { get; }
    }
}
