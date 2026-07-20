using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Defines a thread-safe, single-producer single-consumer ring buffer interface for message passing between concurrent
/// operations.
/// </summary>
/// <remarks>This interface provides methods for enqueuing and dequeuing messages with blocking and cancellation
/// support, as well as lifecycle management for starting and stopping the associated operation. The buffer is intended
/// for scenarios where a single producer and a single consumer interact concurrently. Proper management of the buffer's
/// lifecycle is required to avoid resource leaks and ensure correct operation.</remarks>
/// <typeparam name="TMessage">The value type of messages stored in the ring buffer. Must be a struct to ensure efficient storage and access.</typeparam>
public interface ISpscRingBuffer<TMessage> where TMessage : struct
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
    /// Non-blocking attempt to enqueue a value. Returns false if the buffer is full.
    /// </summary>
    /// <param name="item">The message to enqueue.</param>
    /// <returns><see langword="true"/> if the item was enqueued; <see langword="false"/> if the buffer is full.</returns>
    bool TryEnqueue(in TMessage item);

    /// <summary>
    /// Non-blocking attempt to dequeue a value. Returns false if the buffer is empty.
    /// </summary>
    /// <param name="item">When this method returns <see langword="true"/>, contains the dequeued message.</param>
    /// <returns><see langword="true"/> if an item was dequeued; <see langword="false"/> if the buffer is empty.</returns>
    bool TryDequeue(out TMessage item);

    /// <summary>
    /// Stops the operation or process associated with this instance.
    /// </summary>
    /// <remarks>This method ensures that all resources, such as buffers and synchronization primitives,  are
    /// properly disposed. Once called, the instance cannot be reused.</remarks>
    void Stop();
}
