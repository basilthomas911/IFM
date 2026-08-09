using NATS.Client.Core;


namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents a collection of actor thread queues.
/// </summary>
public interface IActorThreadQueues
{
    bool Write(IActorMessage message, ActorSubject subject, CancellationToken cancellationToken = default);
    ValueTask<bool> WriteAsync(IActorMessage message, CancellationToken cancellationToken = default);
    ValueTask<bool> WriteAsync(IActorMessage message, ActorSubject subject, CancellationToken cancellationToken = default);
    ActorAdmissionResult TryAdmit(
        IActorMessage message,
        ActorSubject subject,
        CancellationToken cancellationToken = default);
    ValueTask<ActorAdmissionResult> TryAdmitAsync(
        IActorMessage message,
        ActorSubject subject,
        CancellationToken cancellationToken = default);
    IActorThreadQueue GetThreadQueue(ActorThreadId threadId);
    bool TryGetThreadQueue(ActorThreadId threadId, out IActorThreadQueue? queue);
    void ReleaseThreadQueue(ActorThreadId threadId);    
    int Count { get; }
}
