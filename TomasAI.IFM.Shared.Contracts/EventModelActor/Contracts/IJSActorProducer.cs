using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines a contract for producing durable actor events through NATS JetStream.
/// </summary>
/// <remarks>Implementations of this interface should ensure thread safety when interacting with actors. The
/// methods provided allow for non-blocking operations, supporting a reactive programming model. The IsRunning property
/// indicates whether the actor producer is currently active.</remarks>
public interface IJSActorProducer
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    ValueTask SendAsync<TEvent, TEntityId>(ActorSubject subject, TEvent @event, CancellationToken cancellationToken)
        where TEvent : class, IEvent<TEntityId>
        where TEntityId : IActorEntityId
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SendAsync<TEvent, TEntityId>(subject, @event);
    }

    ValueTask StartAsync(ActorMailboxId mailboxId);
    ValueTask StartAsync(ActorMailboxId mailboxId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StartAsync(mailboxId);
    }
    ValueTask StopAsync();
    ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return StopAsync();
    }
    bool IsRunning { get; }
}
