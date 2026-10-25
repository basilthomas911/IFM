using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for an actor that processes commands within the event model actor framework.
/// Implementations of this interface handle the lifecycle of the actor,
/// validate commands, load and save state, and process received commands.
/// </summary>
public interface ICommandActor
{
    ValueTask OnStartup(ICommandActorContext context);
    ValueTask OnShutdown(ICommandActorContext context);
    ValueTask OnValidateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command);
    ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command);
    ValueTask OnSaveStateAsync(ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command);
    ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext context, IActorState state, ICommand command);
    ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception ex);
}

/// <summary>
/// Defines the contract for a command actor that is associated with a specific actor type.
/// </summary>
/// <typeparam name="TActor">The concrete command actor type.</typeparam>
public interface ICommandActor<TActor> : ICommandActor, IActor<TActor>
    where TActor : IActor
{
    ValueTask OnStartup(ICommandActorContext<TActor> context);
    ValueTask OnShutdown(ICommandActorContext<TActor> context);
    ValueTask OnValidateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command);
    ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command);
    ValueTask OnSaveStateAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, IActorState state, ICommand command);
    ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<TActor> context, IActorState state, ICommand command);
    ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<TActor> context, ActorThreadId threadId, ICommand command, Exception ex);
}
