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
    ValueTask StartAsync(
        IActorSupervisor context,
        ActorType actorType,
        string consumerName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync(context, actorType, consumerName);
    }
    ValueTask StopAsync();
    ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StopAsync();
    }
    bool IsRunning { get; }
}
