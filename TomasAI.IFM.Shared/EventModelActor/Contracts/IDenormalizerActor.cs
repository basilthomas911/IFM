using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for an actor that processes denormalized events within an actor system.
/// </summary>
/// <remarks>Implementations of this interface are responsible for handling event denormalization, including
/// startup, shutdown, event reception, and exception handling. The interface is typically used in event-driven systems
/// where denormalized views or projections are maintained based on incoming events.</remarks>
/// <typeparam name="TActor">The type of actor implementing the denormalizer functionality. Must implement <see cref="IActor"/>.</typeparam>
public interface IDenormalizerActor<TActor> : IActor<TActor>
    where TActor : IActor
{
    ValueTask OnStartup(IDenormalizerActorContext context);
    ValueTask OnShutdown(IDenormalizerActorContext context);
    ValueTask ReceiveAsync(IDenormalizerActorContext context, ActorThreadId threadId, IEvent @event);
    ValueTask OnExceptionAsync(IDenormalizerActorContext context, ActorThreadId threadId, IEvent @event, Exception ex);
}

