using NATS.Client.Core;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for a JavaScript actor consumer that extends the base actor consumer functionality.
/// </summary>
public interface IJSActorConsumer : IActorConsumer
{
    /// <summary>
    /// Routes an event to a specific actor subject asynchronously.
    /// </summary>
    /// <param name="routeToSubject">The actor subject to which the event should be routed.</param>
    /// <param name="natsMsg">The NATS message containing the event data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask RouteEventToAsync(ActorSubject routeToSubject, NatsMsg<byte[]> natsMsg);
}
