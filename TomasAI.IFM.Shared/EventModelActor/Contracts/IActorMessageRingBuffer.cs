namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines a thread-safe ring buffer for message passing between producers and consumers.
/// </summary>
/// <remarks>The ring buffer provides fixed-capacity storage for messages, supporting both non-blocking and 
/// blocking operations for enqueuing and dequeuing items. It is designed for high-performance  scenarios where
/// producers and consumers operate concurrently. The buffer is optimized for  scenarios where approximate counts and
/// non-blocking operations are sufficient, but also  includes blocking methods for guaranteed operations.</remarks>
public interface IActorMessageRingBuffer<TMessage> where TMessage : struct
{
    /// <summary>
    /// Gets the fixed capacity of the ring buffer.
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Returns the number of elements logically stored in the buffer at this instant.
    /// </summary>
    /// <remarks>
    /// Value is approximate under concurrency and should be used for diagnostics only.
    /// </remarks>
    int Count { get; }

    /// <summary>
    /// True if the buffer contains no items at the instant of the check.
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// True if the buffer cannot accept another item at the instant of the check.
    /// </summary>
    bool IsFull { get; }

   /// <summary>
   /// Attempts to enqueue the specified message into the queue.
   /// </summary>
   /// <remarks>This method does not guarantee that the message will be enqueued successfully. The caller
   /// should handle scenarios where the operation may fail due to queue constraints or cancellation.</remarks>
   /// <param name="item">The message to enqueue. The message is passed by reference to avoid unnecessary copying.</param>
   /// <param name="cancellationToken">A token that can be used to cancel the enqueue operation.</param>
   void TryEnqueue(in TMessage item, CancellationToken? cancellationToken);

    /// <summary>
    /// Attempts to remove and return an item from the queue.
    /// </summary>
    /// <remarks>This method is non-blocking and will immediately return if the queue is empty. If the
    /// operation is canceled via the <paramref name="cancellationToken"/>, the <paramref name="item"/> will not be
    /// modified.</remarks>
    /// <param name="item">When this method returns, contains the item removed from the queue, if the operation was successful; otherwise,
    /// the default value of <typeparamref name="TMessage"/>.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    void TryDequeue(out TMessage item, CancellationToken? cancellationToken);

    /// <summary>
    /// Removes all items currently stored by repeatedly dequeuing. Consumer-only.
    /// </summary>
    void Drain();

    /// <summary>
    /// Efficiently clears the buffer by consuming all items. Consumer-only.
    /// </summary>
    void Clear();

    /// <summary>
    /// Enqueues an item, spinning until space is available. Producer-only.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    void EnqueueSpin(in TMessage item);

    /// <summary>
    /// Dequeues an item, spinning until one is available. Consumer-only.
    /// </summary>
    /// <returns>The dequeued item.</returns>
    TMessage DequeueSpin();
}
