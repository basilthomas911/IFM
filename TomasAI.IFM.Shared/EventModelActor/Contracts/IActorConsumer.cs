using NATS.Client.Core;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines the contract for an actor consumer that manages the lifecycle of actors within a system.
/// </summary>
/// <remarks>An implementation of this interface is responsible for starting and stopping actors, as well as 
/// providing information about the current running state of the actor consumer. This interface is  typically used in
/// actor-based systems to manage actor instances and their execution contexts.</remarks>
public interface IActorConsumer
{
    ValueTask StartAsync(IActorSupervisor context, ActorType actorType, string consumerName = default!);
    ValueTask StopAsync();
    bool IsRunning { get; }
}

/// <summary>
/// Defines the contract for a JavaScript actor consumer that extends the base actor consumer functionality.
/// </summary>
public interface IJSActorConsumer: IActorConsumer
{
    /// <summary>
    /// Routes an event to a specific actor subject asynchronously.
    /// </summary>
    /// <param name="routeToSubject">The actor subject to which the event should be routed.</param>
    /// <param name="natsMsg">The NATS message containing the event data.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask RouteEventToAsync(ActorSubject routeToSubject, NatsMsg<byte[]> natsMsg);
}
