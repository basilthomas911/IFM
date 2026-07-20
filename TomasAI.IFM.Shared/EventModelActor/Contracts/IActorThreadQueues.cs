using NATS.Client.Core;


namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents a collection of actor thread queues.
/// </summary>
public interface IActorThreadQueues
{
    void Write(in NatsMsg<byte[]> message, ActorSubject subject, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(NatsMsg<byte[]> message, ActorSubject subject, CancellationToken cancellationToken = default);
    IActorThreadQueue GetThreadQueue(ActorThreadId threadId);
    void ReleaseThreadQueue(ActorThreadId threadId);    
    int Count { get; }
}
