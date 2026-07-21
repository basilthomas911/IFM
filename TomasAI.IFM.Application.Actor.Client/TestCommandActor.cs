using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Actor.Client;

/// <summary>
/// Represents a command actor for handling and processing test commands within the actor system.
/// </summary>
/// <param name="logger">The logger used to record diagnostic and operational information for the actor.</param>
public class TestCommandActor(ILogger<TestCommandActor> logger) 
    : BaseEventSourceCommandActor<TestCommandActor>(logger, new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "TestCommand";

    /// <summary>
    /// Parses the incoming actor message and resolves the associated command for the actor.
    /// </summary>
    /// <remarks>The method validates the message and attempts to resolve the command based on the message's
    /// subject. If the message subject does not match the expected format for the actor, an <see
    /// cref="InvalidOperationException"/> is thrown.</remarks>
    /// <param name="context">The context in which the command actor operates. This provides access to actor-specific resources and state.</param>
    /// <param name="message">The message to be parsed, containing the subject and payload required to resolve the command.</param>
    /// <exception cref="InvalidOperationException">Thrown if the message subject does not match the expected command format for the actor.</exception>
    protected override ICommand ParseMessage(ICommandActorContext context, in NatsMsg<byte[]> message)
    {
        var msgSubject = message.Subject.ToSubject();
        ICommand command = default(ICommand) switch
        {
            _ when msgSubject.Is(ActorType.Command, ActorName, "TestCommand") => message.AsCommand<TestCommand>()!,
            _ => throw new InvalidOperationException($"Unable to resolve {ActorName} command from message: {message.Subject}")
        };
        return command;
    }

    /// <summary>
    /// Processes an incoming command asynchronously within the specified actor context and state.
    /// </summary>
    /// <param name="context">The context of the command actor, providing access to actor-specific information and services.</param>
    /// <param name="state">The current state of the actor, which may be used or modified during command processing.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    protected override async ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand cmd)
    {
        return await Task.FromResult(new ServiceResult<GuidResult>(new GuidResult(cmd.CommandId)));
    }

    /// <summary>
    /// Asynchronously loads the persisted state for the actor associated with the specified context and thread
    /// identifier.
    /// </summary>
    /// <param name="context">The context for the command actor, providing access to actor metadata and services required for state loading.</param>
    /// <param name="threadId">The identifier of the actor thread for which the state is being loaded.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded actor state.</returns>
    protected override async ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd)
        => await ValueTask.FromResult(new TestCommandState());

    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand cmd, Exception ex)
        => await ValueTask.FromResult(new ServiceResult<GuidResult>(new GuidResult(cmd.CommandId)));
}
