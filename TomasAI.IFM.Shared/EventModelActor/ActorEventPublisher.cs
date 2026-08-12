using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Publishes an actor event exactly once through the transport assigned to its actor type.
/// </summary>
internal sealed class ActorEventPublisher(IActorSupervisor supervisor)
{
    readonly IActorSupervisor _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
    IActorProducer? _coreProducer;
    IJSActorProducer? _jetStreamProducer;

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
                (_coreProducer ??= _supervisor.GetProducer(subject.ActorId))
                    .SendAsync<TEvent, TEntityId>(subject, @event, cancellationToken),
            ActorDeliveryType.NatsJetStream =>
                (_jetStreamProducer ??= _supervisor.GetJSProducer(subject.ActorId))
                    .SendAsync<TEvent, TEntityId>(subject, @event, cancellationToken),
            _ => ValueTask.FromException(new InvalidOperationException(
                $"Actor type '{subject.ActorType}' does not define a delivery transport."))
        };
    }
}
