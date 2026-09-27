using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Actor producer used by API clients without an HTTP serialization hop.</summary>
public interface IApiActorProducer : IActorProducer;

/// <summary>
/// Routes combined/runtime requests through supervisor-owned producers and gives a gateway-only
/// process one explicitly started NATS producer.
/// </summary>
public sealed class ApiActorProducer(
    ApiHostOptions host,
    IServiceProvider services,
    INatsProducerOptions natsOptions,
    ILogger<ApiActorProducer> logger,
    NatsConnectionManager connections) : IApiActorProducer, IHostedService
{
    readonly NatsActorProducer? gatewayProducer = host.Role == ApiHostRole.Gateway
        ? new NatsActorProducer(natsOptions, logger, connections)
        : null;

    IActorProducer Producer(ActorSubject subject) => gatewayProducer
        ?? services.GetRequiredService<IActorSupervisor>().GetProducer(subject.ActorId);

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
        => gatewayProducer is null
            ? Task.CompletedTask
            : gatewayProducer.StartAsync(
                new ActorMailboxId(ActorType.Query, nameof(ApiActorProducer)),
                cancellationToken).AsTask();

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
        => gatewayProducer is null
            ? Task.CompletedTask
            : gatewayProducer.StopAsync(cancellationToken).AsTask();

    public bool IsRunning => gatewayProducer?.IsRunning
        ?? services.GetRequiredService<IActorSupervisor>().IsReady;

    public ValueTask StartAsync(ActorMailboxId mailboxId)
        => gatewayProducer?.StartAsync(mailboxId) ?? ValueTask.CompletedTask;

    public ValueTask StartAsync(ActorMailboxId mailboxId, CancellationToken cancellationToken)
        => gatewayProducer?.StartAsync(mailboxId, cancellationToken)
            ?? (cancellationToken.IsCancellationRequested
                ? ValueTask.FromCanceled(cancellationToken)
                : ValueTask.CompletedTask);

    public ValueTask StopAsync()
        => gatewayProducer?.StopAsync() ?? ValueTask.CompletedTask;

    public ValueTask StopAsync(CancellationToken cancellationToken)
        => gatewayProducer?.StopAsync(cancellationToken)
            ?? (cancellationToken.IsCancellationRequested
                ? ValueTask.FromCanceled(cancellationToken)
                : ValueTask.CompletedTask);

    public ValueTask SendAsync<TCommand, TEntityId>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        => Producer(subject).SendAsync(subject, command, entityId);

    public ValueTask SendAsync<TCommand, TEntityId>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        => Producer(subject).SendAsync(subject, command, entityId, cancellationToken);

    public ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
        => Producer(subject).SendAsync<TEvent, TEntityId>(subject, @event);

    public ValueTask SendAsync<TEvent, TEntityId>(
        ActorSubject subject,
        TEvent @event,
        CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
        => Producer(subject).SendAsync<TEvent, TEntityId>(subject, @event, cancellationToken);

    public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(
        ActorSubject subject,
        TQuery query)
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => Producer(subject).RequestAsync<TResult, TQuery>(subject, query);

    public ValueTask<ServiceResult<TResult>> RequestAsync<TResult, TQuery>(
        ActorSubject subject,
        TQuery query,
        CancellationToken cancellationToken)
        where TQuery : class, IQuery<TResult>
        where TResult : class
        => Producer(subject).RequestAsync<TResult, TQuery>(subject, query, cancellationToken);

    public ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class
        => Producer(subject).RequestAsync<TCommand, TEntityId, TResult>(
            subject,
            command,
            entityId);

    public ValueTask<ServiceResult<TResult>> RequestAsync<TCommand, TEntityId, TResult>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class
        => Producer(subject).RequestAsync<TCommand, TEntityId, TResult>(
            subject,
            command,
            entityId,
            cancellationToken);

    public ValueTask<ServiceResult<TResult>> RequestFunctionAsync<TCommand, TEntityId, TResult>(
        ActorSubject subject,
        TCommand command,
        TEntityId entityId,
        CancellationToken cancellationToken = default)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
        where TResult : class
        => Producer(subject).RequestFunctionAsync<TCommand, TEntityId, TResult>(
            subject,
            command,
            entityId,
            cancellationToken);
}
