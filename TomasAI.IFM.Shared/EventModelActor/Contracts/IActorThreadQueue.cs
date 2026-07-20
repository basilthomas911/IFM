using NATS.Client.Core;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents a thread-safe queue for processing actor messages, providing methods to manage the queue's lifecycle and
/// interact with its contents.
/// </summary>
/// <remarks>This interface is designed to facilitate message handling in actor-based systems. It allows for
/// setting an identifier, reading messages asynchronously,  writing messages to the queue, and controlling the queue's
/// operational state.</remarks>
public interface IActorThreadQueue 
{
    IActorThreadQueue SetId(ActorThreadId id);
    ActorThreadId Id { get; }
    int Count { get; }
    IAsyncEnumerable<NatsMsg<byte[]>> ReadAllAsync(CancellationToken cancellationToken = default);
    IEnumerable<NatsMsg<byte[]>> ReadAll(CancellationToken cancellationToken = default);
    bool Write(in NatsMsg<byte[]> message, CancellationToken cancellationToken = default);
    ValueTask EnqueueAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default);
    void Start();
    void Stop();
}

public interface IActorThreadQueue<TQueue> : IActorThreadQueue 
    where TQueue : IActorThreadQueue
{ 
}
