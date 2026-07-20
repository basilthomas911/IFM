using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for a command-based actor that processes messages, manages state, and handles lifecycle events.
/// </summary>
/// <remarks>This interface provides methods for handling the actor's lifecycle events, message processing, state
/// management, and exception handling. Implementations are expected to define the behavior for each of these
/// operations in the context of a command-driven actor system.</remarks>
/// <typeparam name="TActor">The type of the actor implementing this interface. Must derive from <see cref="IActor"/>.</typeparam>
public interface ICommandActor<TActor> : IActor<TActor>
    where TActor : IActor
{
    ValueTask OnStartup(ICommandActorContext context);
    ValueTask OnShutdown(ICommandActorContext context);
    ValueTask OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command);
    ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command);
    ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command);
    ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand command);
    ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex);
}
