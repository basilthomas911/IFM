using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents an actor that processes commands and participates in the actor lifecycle.
/// </summary>
/// <remarks>
/// A command actor is responsible for receiving messages, validating and executing commands,
/// managing its internal state, and handling startup/shutdown and error conditions.  This
/// interface exposes the core actor operations as well as references to the mailbox, state
/// and the producer/consumer used to communicate with the actor runtime.
/// </remarks>
public interface ICommandActor
{
    /// <summary>
    /// Process an incoming actor message asynchronously.
    /// </summary>
    /// <param name="context">The actor context used to interact with other actors and runtime services.</param>
    /// <param name="message">The message to process.</param>
    ValueTask ReceiveAsync(IActorContext context, IActorMessage message);

    /// <summary>
    /// Start the actor and prepare it to receive messages.
    /// </summary>
    /// <param name="context">The actor context that provides runtime services and access to other actors.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the actor has started.</returns>
    ValueTask StartAsync(IActorContext context);

    /// <summary>
    /// Stop the actor and perform any required cleanup.
    /// </summary>
    /// <param name="context">The actor context for the actor being stopped.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the actor has stopped.</returns>
    ValueTask StopAsync(IActorContext context);

    /// <summary>
    /// The mailbox assigned to this actor used for receiving messages.
    /// </summary>
    IActorMailbox Mailbox { get; }

    /// <summary>
    /// The mailbox used by external callers to send request messages to this actor.
    /// </summary>
    IActorMailbox RequestMailbox { get; }

    /// <summary>
    /// The current actor state object. Implementations should expose their runtime state via this property.
    /// </summary>
    IActorState State { get; }

    /// <summary>
    /// True when the actor is currently running and able to receive messages.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// True when the actor is a parent that owns child actors.
    /// </summary>
    bool IsParent { get; }

}

/// <summary>
/// Generic command actor interface that provides lifecycle and message handling hooks intended to be
/// implemented or overridden by concrete actor types.
/// </summary>
/// <typeparam name="TActor">The concrete actor type implementing this interface.</typeparam>
public interface ICommandActor<TActor> : ICommandActor
    where TActor : ICommandActor
{
    /// <summary>
    /// Hook invoked during actor startup. Implementations can initialize resources here.
    /// </summary>
    /// <param name="context">The actor context providing runtime services.</param>
    ValueTask OnStartup(IActorContext context);

    /// <summary>
    /// Hook invoked during actor shutdown. Implementations can release resources here.
    /// </summary>
    /// <param name="context">The actor context providing runtime services.</param>
    ValueTask OnShutdown(IActorContext context);

    /// <summary>
    /// Hook invoked to handle an incoming message. Implementations should implement message routing
    /// and command handling here.
    /// </summary>
    /// <param name="context">The actor context.</param>
    /// <param name="message">The message being received.</param>
    ValueTask OnReceiveAsync(IActorContext context, IActorMessage message);

    /// <summary>
    /// Hook invoked to validate a command prior to execution. Implementations should throw or
    /// return a failed result for invalid commands.
    /// </summary>
    /// <param name="context">The actor context.</param>
    /// <param name="command">The command to validate.</param>
    ValueTask OnValidateAsync(IActorContext context, ICommand command);

    /// <summary>
    /// Load the actor's state from storage or other backing store.
    /// </summary>
    /// <param name="context">The actor context.</param>
    /// <returns>The loaded <see cref="IActorState"/> instance.</returns>
    ValueTask<IActorState> OnLoadStateAsync(IActorContext context);

    /// <summary>
    /// Persist the actor's current state to storage.
    /// </summary>
    /// <param name="context">The actor context.</param>
    /// <param name="state">The state to save.</param>
    ValueTask OnSaveStateAsync(IActorContext context, IActorState state);

    /// <summary>
    /// Handle an exception that occurred during actor processing. Implementations can log, compensate,
    /// or rethrow as appropriate for the actor's error policy.
    /// </summary>
    /// <param name="context">The actor context.</param>
    /// <param name="ex">The exception to handle.</param>
    ValueTask OnExceptionAsync(IActorContext context, Exception ex);
}

