using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents the context for an event-driven actor, providing methods for sending messages,  querying state, and
/// managing actor lifecycle and relationships.
/// </summary>
/// <remarks>This interface defines the contract for interacting with an actor's context, including  sending
/// events and commands, querying state, checking actor existence, and managing child actors.  It is designed to
/// facilitate communication and lifecycle management in an actor-based system.</remarks>
public interface IEventActorContext
{
    ActorMailboxId ActorId { get; }
    IContainerInstance Container { get; }
    ISupervisorRuntimeContext SupervisorRuntime => null!;
    /// <summary>Gets whether the supervised actor runtime is still accepting normal work.</summary>
    bool IsReady => true;

    bool SetMessageInfo(ActorThreadId threadId, ActorMessageInfo info);
    ActorMessageInfo? GetMessageInfo(ActorThreadId threadId);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId;

    ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class;

    ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId;

    /// <summary>Executes a command-shaped Function request and returns its typed result.</summary>
    ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(
        TCommand command,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class;

    public void AddEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId);
    public void RemoveEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId);
    public void AddRealtimeRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId);
    public void AddRealtimeRouter(
        ActorTypeId fromActorTypeId,
        ActorMailboxId toMailboxId,
        Func<ActorSubject, string> entityIdProjection);
    public void RemoveRealtimeRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId);

}

/// <summary>
/// Provides an event actor context associated with a specific actor type.
/// </summary>
/// <typeparam name="TActor">The event actor type associated with the context.</typeparam>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
public interface IEventActorContext<TActor> : IEventActorContext
    where TActor : IActor
{
}
