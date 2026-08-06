namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for an actor that processes messages and manages its lifecycle within an actor-based system.
/// </summary>
/// <remarks>An implementation of <see cref="IActor"/> is responsible for handling incoming messages,
/// managing its state, and interacting with its environment through the provided context. The actor's lifecycle is
/// managed via the <see cref="StartAsync"/> and <see cref="StopAsync"/> methods, and its behavior is defined by the
/// <see cref="HandleMessageAsync"/> method.</remarks>
public interface IActor
{
    ActorMailboxId Id { get; }

    /// <summary>
    /// Handle an incoming message asynchronously.
    /// </summary>
    /// <param name="message">The message to process.</param>
    ValueTask HandleMessageAsync(IActorMessage message);

    /// <summary>
    /// Handle an incoming message with a pre-resolved thread identifier, avoiding redundant subject parsing.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="threadId">The pre-resolved thread identifier from the caller.</param>
    ValueTask HandleMessageAsync(IActorMessage message, ActorThreadId threadId)
        => HandleMessageAsync(message);

    /// <summary>
    /// Handles an incoming message with cancellation for work that has not crossed its durable commit boundary.
    /// Implementations must not abandon a partially persisted command merely because this token is cancelled.
    /// </summary>
    ValueTask HandleMessageAsync(
        IActorMessage message,
        ActorThreadId threadId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return HandleMessageAsync(message, threadId);
    }

    /// <summary>
    /// Starts the actor asynchronously, initializing its components and setting it to a running state.
    /// </summary>
    /// <remarks>This method initializes the actor's mailbox and transitions the actor to a running state.
    /// It must be called only when the actor is not already running.</remarks>
    /// <param name="supervisor">The actor supervisor that provides runtime information and services for the actor.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask StartAsync(IActorSupervisor supervisor);

    /// <summary>Starts the actor while allowing startup waits and external I/O to be cancelled.</summary>
    ValueTask StartAsync(IActorSupervisor supervisor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync(supervisor);
    }

    /// <summary>
    /// Stops the actor asynchronously, performing any necessary cleanup operations.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous stop operation.</returns>
    ValueTask StopAsync();

    /// <summary>
    /// Gracefully stops the actor. The token bounds the caller's wait; accepted commands are drained to a safe
    /// persistence boundary rather than being abandoned.
    /// </summary>
    ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StopAsync();
    }

    /// <summary>
    /// Gets the actor's mailbox.
    /// </summary>
    IActorMailbox Mailbox { get; }

    /// <summary>
    /// Whether the actor is currently running.
    /// </summary>
    bool IsRunning { get; }
}

public interface IActor<TActor> : IActor where TActor : IActor
{
}

