using  NATS.Client.Core;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// Represents a thread-safe queue for actor messages, designed to integrate with a single-producer, single-consumer
/// (SPSC) ring buffer.
/// </summary>
/// <remarks>This class provides functionality for managing actor messages in a high-performance, thread-safe
/// manner. It supports asynchronous message reading, message writing, and lifecycle management (start and stop) for the
/// underlying ring buffer. The queue is intended to be used in conjunction with an <see cref="IActorSupervisor"/> to
/// coordinate actor message processing.</remarks>
/// <param name="actorSupervisor"></param>
public class NatsActorThreadQueue(IActorSupervisor actorSupervisor)
    : IActorThreadQueue
{
    readonly IActorSupervisor _actorSupervisor = IsArgumentNull.Set(actorSupervisor);
    ActorThreadId _id = default!;
    IActorSpscRingBuffer<NatsMsg<byte[]>> _buffer = default!;

    /// <summary>
    /// Gets the unique identifier for the actor thread.
    /// </summary>
    public ActorThreadId Id => _id;

    /// <summary>
    /// Gets the number of elements currently contained in the buffer.
    /// </summary>
    /// <remarks>This property provides a quick way to determine the current size of the buffer. The value
    /// updates automatically as items are added to or removed from the buffer.</remarks>
    public int Count => _buffer.Count;

    /// <summary>
    /// Sets the identifier for the actor thread queue.
    /// </summary>
    /// <param name="id">The <see cref="ActorThreadId"/> to assign to the actor thread queue. Cannot be <c>null</c>.</param>
    /// <returns>The current instance of <see cref="IActorThreadQueue"/> to allow method chaining.</returns>
    public IActorThreadQueue SetId(ActorThreadId id)
    {
        _id = IsArgumentNull.Set(id);
        return this;
    }

    /// <summary>
    /// Asynchronously reads all actor messages from the source and returns them as an asynchronous stream.
    /// </summary>
    /// <remarks>The method continues reading messages until no more messages are available. If the operation
    /// is canceled via the <paramref name="cancellationToken"/>, the enumeration will stop and no further messages will
    /// be yielded.</remarks>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation. The default value is <see
    /// cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that yields <see cref="IActorMessage"/> instances as they are read.</returns>
    public async IAsyncEnumerable<NatsMsg<byte[]>> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (Read(out var natsMsg, cancellationToken))
        {
            yield return natsMsg;
        }
        await Task.CompletedTask;

        bool Read(out NatsMsg<byte[]> natsMsg, CancellationToken cancellationToken = default)
        {
            natsMsg = default!;
            if (!cancellationToken.IsCancellationRequested)
            {
                natsMsg = _buffer.Dequeue(cancellationToken);
                return true;
            }
            return false;
        }

    }

    /// <summary>
    /// Reads and returns all available messages from the buffer as an enumerable sequence.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the read operation.</param>
    /// <returns>An enumerable collection of messages read from the buffer. The sequence ends when no more messages are available
    /// or the operation is canceled.</returns>
    public IEnumerable<NatsMsg<byte[]>> ReadAll(CancellationToken cancellationToken = default)
    {
        while (Read(out var natsMsg, cancellationToken))
        {
            yield return natsMsg;
        }

        bool Read(out NatsMsg<byte[]> natsMsg, CancellationToken cancellationToken = default)
        {
            natsMsg = default!;
            if (!cancellationToken.IsCancellationRequested)
            {
                natsMsg = _buffer.Dequeue(cancellationToken);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Starts the processing of the single-producer, single-consumer ring buffer.
    /// </summary>
    /// <remarks>This method initializes and begins the operation of the underlying ring buffer, enabling
    /// message processing. Ensure that the necessary dependencies are resolved before calling this method.</remarks>
    public void Start()
    {
       _buffer = _actorSupervisor.Container.Resolve<IActorSpscRingBuffer<NatsMsg<byte[]>>>();
        _buffer.Start();
    }

    /// <summary>
    /// Stops the operation of the buffer, halting any ongoing processes.
    /// </summary>
    /// <remarks>This method ensures that the buffer ceases all activity. It should be called when the buffer
    /// is no longer needed or before disposing of the containing object.</remarks>
    public void Stop()
    {
        _buffer.Stop();
    }

    /// <summary>
    /// Enqueues the specified actor message into the internal buffer.
    /// </summary>
    /// <remarks>The method processes the provided <see cref="IActorMessage"/> by converting it to a
    /// NATS-compatible message and adding it to the internal buffer. The caller is responsible for ensuring the message
    /// is valid.</remarks>
    /// <param name="message">The actor message to be enqueued. Must implement <see cref="IActorMessage"/>.</param>
    /// <returns><see langword="true"/> if the message was successfully enqueued.</returns>
    public bool Write(in NatsMsg<byte[]> message)
    {
        _buffer.Enqueue(message);
        return true;
    }

    public bool Write(in NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
    {
        _buffer.Enqueue(message, cancellationToken);
        return true;
    }

    public ValueTask EnqueueAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
    {
        _buffer.Enqueue(message);
        return ValueTask.CompletedTask;
    }
    public void SetMessageAvailable()
    {
        // This method can be used to signal that a message is available for processing, if needed.
        // In this implementation, the channel's built-in mechanisms handle message availability, so this method is currently a no-op.
        throw new NotImplementedException();
    }
}
