
namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents a thread-safe queue for processing actor messages, providing methods to manage the queue's lifecycle and
/// interact with its contents.
/// </summary>
/// <remarks>This interface is designed to facilitate message handling in actor-based systems. It allows for
/// setting an identifier, reading messages asynchronously,  writing messages to the queue, and controlling the queue's
/// operational state.</remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "The established public actor queue/handler contract name describes its role and must remain compatible.")]
public interface IActorThreadQueue
{
    IActorThreadQueue SetId(ActorThreadId id);
    ActorThreadId Id { get; }
    int Count { get; }
    int Capacity { get; }
    IAsyncEnumerable<IActorMessage> ReadAllAsync(CancellationToken cancellationToken = default);
    IEnumerable<IActorMessage> ReadAll(CancellationToken cancellationToken = default);
    bool Write(IActorMessage message, CancellationToken cancellationToken = default);
    ValueTask EnqueueAsync(IActorMessage message, CancellationToken cancellationToken = default);
    void Start();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public virtual/interface member and named argument contract used by actor implementations.")]
    void Stop();
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2326", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1711", Justification = "The established public actor queue/handler contract name describes its role and must remain compatible.")]
public interface IActorThreadQueue<TQueue> : IActorThreadQueue
    where TQueue : IActorThreadQueue
{
}
