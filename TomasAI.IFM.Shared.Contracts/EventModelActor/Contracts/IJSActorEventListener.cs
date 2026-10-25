using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Identifies an actor event listener that receives events through a durable
/// NATS JetStream consumer.
/// </summary>
/// <remarks>
/// This distinct interface allows a process to resolve both the transient Core
/// NATS listener and the durable JetStream listener through dependency injection.
/// </remarks>
public interface IJSActorEventListener : IActorEventListener
{
}


