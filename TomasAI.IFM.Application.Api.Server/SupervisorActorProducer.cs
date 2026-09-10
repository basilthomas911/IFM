using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Routes application-service requests through the producers already owned and started by the
/// actor supervisor.  This avoids creating an unstarted, second NATS producer for in-process APIs.
/// </summary>
public sealed class SupervisorActorProducer(IActorSupervisor supervisor) : IActorProducer
{
    IActorProducer Producer(ActorSubject subject) => supervisor.GetProducer(subject.ActorId);

    public ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId =>
        Producer(subject).SendAsync(subject, command, entityId);

    public ValueTask SendAsync<TCommand, TEntityId>(ActorSubject subject, TCommand command, TEntityId entityId, CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId =>
        Producer(subject).SendAsync(subject, command, entityId, cancellationToken);

    public ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
        where TEvent : class, IEvent<TEntityId> where TEntityId : IActorEntityId =>
        Producer(subject).SendAsync<TEvent, TEntityId>(subject, @event);

    public ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event, CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId> where TEntityId : IActorEntityId =>
        Producer(subject).SendAsync<TEvent, TEntityId>(subject, @event, cancellationToken);

    public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query)
        where TQuery : class, IQuery<TResult> where TResult : class =>
        Producer(subject).RequestAsync<TResult, TQuery>(subject, query);

    public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(ActorSubject subject, TQuery query, CancellationToken cancellationToken)
        where TQuery : class, IQuery<TResult> where TResult : class =>
        Producer(subject).RequestAsync<TResult, TQuery>(subject, query, cancellationToken);

    public ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId)
        where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class =>
        Producer(subject).RequestAsync<TCommand, TEntityId, TResult>(subject, command, entityId);

    public ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId, CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class =>
        Producer(subject).RequestAsync<TCommand, TEntityId, TResult>(subject, command, entityId, cancellationToken);

    public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(ActorSubject subject, TCommand command, TEntityId entityId, CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TEntityId> where TEntityId : IActorEntityId where TResult : class =>
        Producer(subject).RequestFunctionAsync<TCommand, TEntityId, TResult>(subject, command, entityId, cancellationToken);

    public ValueTask StartAsync(ActorMailboxId mailboxId) => ValueTask.CompletedTask;
    public ValueTask StartAsync(ActorMailboxId mailboxId, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken) : ValueTask.CompletedTask;
    public ValueTask StopAsync() => ValueTask.CompletedTask;
    public ValueTask StopAsync(CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken) : ValueTask.CompletedTask;
    public bool IsRunning => supervisor.IsReady;
}
