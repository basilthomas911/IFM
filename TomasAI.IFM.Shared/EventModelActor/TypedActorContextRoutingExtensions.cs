using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Routes operations from actor-owned typed contexts through the runtime contexts supplied by the unchanged shared
/// actor bases during the incremental typed-context migration.
/// </summary>
public static class TypedActorContextRoutingExtensions
{
    /// <summary>Creates a typed command context whose operations are forwarded to the runtime context.</summary>
    public static ICommandActorContext<TActor> RouteTo<TActor>(
        this ICommandActorContext<TActor> ownedContext,
        ICommandActorContext runtimeContext)
        where TActor : IActor
        => new RoutedCommandActorContext<TActor>(ownedContext, runtimeContext);

    /// <summary>Creates a typed event context whose operations are forwarded to the runtime context.</summary>
    public static IEventActorContext<TActor> RouteTo<TActor>(
        this IEventActorContext<TActor> ownedContext,
        IEventActorContext runtimeContext)
        where TActor : IActor
        => new RoutedEventActorContext<TActor>(ownedContext, runtimeContext);

    /// <summary>Creates a typed query context whose operations are forwarded to the runtime context.</summary>
    public static IQueryActorContext<TActor> RouteTo<TActor>(
        this IQueryActorContext<TActor> ownedContext,
        IQueryActorContext runtimeContext)
        where TActor : IActor
        => new RoutedQueryActorContext<TActor>(ownedContext, runtimeContext);

    sealed class RoutedCommandActorContext<TActor>(
        ICommandActorContext<TActor> ownedContext,
        ICommandActorContext runtimeContext) : ICommandActorContext<TActor>
        where TActor : IActor
    {
        readonly ICommandActorContext<TActor> _ownedContext = IsArgumentNull.Set(ownedContext);
        readonly ICommandActorContext _runtimeContext = IsArgumentNull.Set(runtimeContext, "context")!;

        /// <summary>Gets the ActorId value.</summary>
        public ActorMailboxId ActorId => _ownedContext.ActorId;
        /// <summary>Gets the Container value.</summary>
        public IContainerInstance Container => _ownedContext.Container;
        /// <summary>Executes the SendAsync operation.</summary>
        public ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
            where TEvent : class, IEvent<TEntityId>
            where TEntityId : IActorEntityId => _runtimeContext.SendAsync<TEvent, TEntityId>(@event);
        /// <summary>Executes the SendAsync operation.</summary>
        public ValueTask SendAsync<TEvent, TEntityId>(TEvent @event, CancellationToken cancellationToken)
            where TEvent : class, IEvent<TEntityId>
            where TEntityId : IActorEntityId => _runtimeContext.SendAsync<TEvent, TEntityId>(@event, cancellationToken);
        /// <summary>Executes the SetMessageInfo operation.</summary>
        public bool SetMessageInfo(ActorThreadId threadId, string verb, ActorMessageInfo info)
            => _runtimeContext.SetMessageInfo(threadId, verb, info);
        /// <summary>Executes the GetMessageInfo operation.</summary>
        public ActorMessageInfo? GetMessageInfo(ActorThreadId threadId, string verb)
            => _runtimeContext.GetMessageInfo(threadId, verb);
    }

    sealed class RoutedEventActorContext<TActor>(
        IEventActorContext<TActor> ownedContext,
        IEventActorContext runtimeContext) : IEventActorContext<TActor>
        where TActor : IActor
    {
        readonly IEventActorContext<TActor> _ownedContext = IsArgumentNull.Set(ownedContext);
        readonly IEventActorContext _runtimeContext = IsArgumentNull.Set(runtimeContext, "context")!;

        /// <summary>Gets the ActorId value.</summary>
        public ActorMailboxId ActorId => _ownedContext.ActorId;
        /// <summary>Gets the Container value.</summary>
        public IContainerInstance Container => _ownedContext.Container;
        /// <summary>Executes the SetMessageInfo operation.</summary>
        public bool SetMessageInfo(ActorThreadId threadId, ActorMessageInfo info)
            => _runtimeContext.SetMessageInfo(threadId, info);
        /// <summary>Executes the GetMessageInfo operation.</summary>
        public ActorMessageInfo? GetMessageInfo(ActorThreadId threadId) => _runtimeContext.GetMessageInfo(threadId);
        /// <summary>Executes the SendAsync operation.</summary>
        public ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
            where TEvent : class, IEvent<TEntityId>
            where TEntityId : IActorEntityId => _runtimeContext.SendAsync<TEvent, TEntityId>(@event);
        /// <summary>Executes the SendAsync operation.</summary>
        public ValueTask SendAsync<TCommand, TEntityId>(TCommand command, TEntityId entityId)
            where TCommand : class, ICommand<TEntityId>
            where TEntityId : IActorEntityId => _runtimeContext.SendAsync(command, entityId);
        /// <summary>Executes the RequestAsync operation.</summary>
        public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(TQuery query)
            where TQuery : class, IQuery<TResult>
            where TResult : class => _runtimeContext.RequestAsync<TResult, TQuery>(query);
        /// <summary>Executes the RequestAsync operation.</summary>
        public ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
            where TCommand : class, ICommand<TEntityId>
            where TEntityId : IActorEntityId => _runtimeContext.RequestAsync<TCommand, TEntityId>(command);
        /// <summary>Executes the AddEventRouter operation.</summary>
        public void AddEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId)
            => _runtimeContext.AddEventRouter(fromActorTypeId, toMailboxId);
        /// <summary>Executes the RemoveEventRouter operation.</summary>
        public void RemoveEventRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId)
            => _runtimeContext.RemoveEventRouter(fromActorTypeId, toMailboxId);
        /// <summary>Executes the AddRealtimeRouter operation.</summary>
        public void AddRealtimeRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId)
            => _runtimeContext.AddRealtimeRouter(fromActorTypeId, toMailboxId);
        /// <summary>Executes the RemoveRealtimeRouter operation.</summary>
        public void RemoveRealtimeRouter(ActorTypeId fromActorTypeId, ActorMailboxId toMailboxId)
            => _runtimeContext.RemoveRealtimeRouter(fromActorTypeId, toMailboxId);
    }

    sealed class RoutedQueryActorContext<TActor>(
        IQueryActorContext<TActor> ownedContext,
        IQueryActorContext runtimeContext) : IQueryActorContext<TActor>
        where TActor : IActor
    {
        readonly IQueryActorContext<TActor> _ownedContext = IsArgumentNull.Set(ownedContext);
        readonly IQueryActorContext _runtimeContext = IsArgumentNull.Set(runtimeContext, "context")!;

        /// <summary>Gets the ActorId value.</summary>
        public ActorMailboxId ActorId => _ownedContext.ActorId;
        /// <summary>Gets the Container value.</summary>
        public IContainerInstance Container => _ownedContext.Container;
        /// <summary>Executes the SendAsync operation.</summary>
        public ValueTask SendAsync<TEvent, TEntityId>(TEvent @event)
            where TEvent : class, IEvent<TEntityId>
            where TEntityId : IActorEntityId => _runtimeContext.SendAsync<TEvent, TEntityId>(@event);
        /// <summary>Executes the SetMessageInfo operation.</summary>
        public bool SetMessageInfo(ActorThreadId threadId, string verb, ActorMessageInfo info)
            => _runtimeContext.SetMessageInfo(threadId, verb, info);
        /// <summary>Executes the GetMessageInfo operation.</summary>
        public ActorMessageInfo? GetMessageInfo(ActorThreadId threadId, string verb)
            => _runtimeContext.GetMessageInfo(threadId, verb);
        /// <summary>Executes the TakeMessageInfo operation.</summary>
        public ActorMessageInfo? TakeMessageInfo(ActorThreadId threadId, string verb)
            => _runtimeContext.TakeMessageInfo(threadId, verb);
        /// <summary>Executes the RemoveMessageInfo operation.</summary>
        public bool RemoveMessageInfo(ActorThreadId threadId, string verb)
            => _runtimeContext.RemoveMessageInfo(threadId, verb);
        /// <summary>Executes the ReplyAsync operation.</summary>
        public ValueTask ReplyAsync<TResult>(ActorThreadId threadId, string verb, ServiceResult<TResult> replyResult)
            => _runtimeContext.ReplyAsync(threadId, verb, replyResult);
    }
}
