using System;
using System.Threading;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Represents a single-producer, single-consumer (SPSC) ring buffer for message passing between actors.
/// </summary>
/// <remarks>This interface provides methods for enqueuing and dequeuing messages in a thread-safe manner, with
/// blocking operations that can be cancelled via a <see cref="CancellationToken"/>. The buffer's capacity is a power of
/// two, and the <see cref="Count"/> property provides an approximate count of items, useful for diagnostics.</remarks>
/// <typeparam name="TMessage">The type of messages stored in the buffer. Must be a value type.</typeparam>
public interface IActorSpscRingBuffer<TMessage> where TMessage : struct
{
    /// <summary>
    /// Gets the logical capacity of the ring buffer (power of two).
    /// </summary>
    int Capacity { get; }

    /// <summary>
    /// Gets an approximate item count computed from producer/consumer indices (diagnostics only).
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Enqueues a message, blocking (cancellable) when the buffer is full.
    /// </summary>
    /// <param name="item">The message to enqueue.</param>
    /// <param name="cancellationToken">A token to observe while waiting for space to become available.</param>
    void Enqueue(in TMessage item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues and returns the next message, blocking (cancellable) when the buffer is empty.
    /// </summary>
    /// <param name="cancellationToken">A token to observe while waiting for an item to become available.</param>
    /// <returns>The dequeued message.</returns>
    TMessage Dequeue(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the operation or process associated with this instance.
    /// </summary>
    /// <remarks>This method initiates the operation and transitions the instance to a running state.  Ensure
    /// that all required preconditions are met before calling this method.</remarks>
    void Start();
    
    /// <summary>
    /// Stops the operation or process associated with this instance.
    /// </summary>
    /// <remarks>This method ensures that all resources, such as buffers and synchronization primitives,  are
    /// properly disposed. Once called, the instance cannot be reused.</remarks>
    void Stop();
}