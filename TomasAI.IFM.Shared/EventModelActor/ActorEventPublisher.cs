using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Publishes an actor event exactly once through the transport assigned to its actor type.
/// </summary>
internal sealed class ActorEventPublisher(
    IActorSupervisor supervisor,
    ActorMailboxId publisherId)
{
    readonly IActorSupervisor _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
    readonly ActorMailboxId _publisherId = publisherId;
    IActorProducer? _coreProducer;
    IActorProducer? _notifyProducer;

    internal ValueTask SendAsync<TEvent, TEntityId>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        ArgumentNullException.ThrowIfNull(@event);
        var subject = @event.Subject;
        return subject.ActorType.GetDeliveryType() switch
        {
            ActorDeliveryType.NatsCore =>
                (subject.ActorType == ActorType.Notify
                    ? (_notifyProducer ??= _supervisor.GetProducer(_publisherId))
                    : (_coreProducer ??= _supervisor.GetProducer(subject.ActorId)))
                    .SendAsync<TEvent, TEntityId>(subject, @event, cancellationToken),
            ActorDeliveryType.NatsJetStream =>
                SendJetStreamAsync<TEvent, TEntityId>(subject, @event, cancellationToken),
            _ => ValueTask.FromException(new InvalidOperationException(
                $"Actor type '{subject.ActorType}' does not define a delivery transport."))
        };
    }

    async ValueTask SendJetStreamAsync<TEvent, TEntityId>(
        ActorSubject subject,
        TEvent @event,
        CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        var actorExists = _supervisor.ActorExists(subject.ActorId);
        var producer = actorExists
            ? _supervisor.GetJSProducer(subject.ActorId)
            : _supervisor.GetJSEventProducer(subject.ActorId);

        // Durable service/execution events deliberately have no destination actor.
        // Their supervisor-owned producer therefore has no actor startup path and
        // must be started at first publication. Actor-owned producers are already
        // started with their actor.
        if (!actorExists)
            await producer.StartAsync(subject.ActorId, cancellationToken).ConfigureAwait(false);

        await producer.SendAsync<TEvent, TEntityId>(subject, @event, cancellationToken).ConfigureAwait(false);
    }
}
