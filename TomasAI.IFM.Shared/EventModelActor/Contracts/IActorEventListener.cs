using NATS.Client.Core;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines a contract for listening to and handling events published by actor mailboxes.
/// </summary>
/// <remarks>Implementations of this interface are responsible for subscribing to events from one or more actor
/// mailboxes and invoking the provided event handler when events are received. The interface allows clients to manage
/// the listener's lifecycle and monitor its current state.</remarks>
public interface IActorEventListener
{
    public EventListenerState State { get; }
    public int MessageCount { get; }

    public ValueTask StartAsync(string eventListenerId, Dictionary<ActorMailboxId, List<string>> eventMap, Func<string, NatsMsg<byte[]>, ValueTask> eventHandler);
    public ValueTask StopAsync();

}


