using System.Runtime.CompilerServices;
using NATS.Client.Core;

using TomasAI.IFM.Framework.Messaging.Nats.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// A high-performance, <b>single-producer single-consumer</b> (SPSC) ring buffer.
/// <para>
/// Characteristics:
/// <list type="bullet">
/// <item><description><b>GC-free during steady state:</b> no allocations after construction. Slots are reused, and dequeues clear references to avoid retention.</description></item>
/// <item><description><b>Thread-safe for SPSC:</b> exactly one producer thread may call <see cref="TryEnqueue"/>, and exactly one consumer thread may call <see cref="TryDequeue"/> concurrently.</description></item>
/// <item><description><b>Non-blocking:</b> operations return immediately; no locks, no spins beyond simple CPU progress, and minimal fences via <see cref="Volatile"/>.</description></item>
/// <item><description><b>Power-of-two capacity:</b> enables fast index masking.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>IMPORTANT:</b> This implementation is <i>not</i> safe for multiple producers or multiple consumers.
/// If you need MPMC, use a different algorithm (e.g., Michael-Scott queue) or a channel library.
/// </para>
/// </summary>

public sealed class NatsActorMessageRingBuffer : IActorMessageRingBuffer<NatsMsg<byte[]>>
{
    readonly NatsMsg<byte[]>[] _buffer;
    readonly int _mask; // capacity - 1

    // Head (write index) and tail (read index). Only producer mutates _head; only consumer mutates _tail.
    // Marked volatile so reads/writes have acquire/release semantics through Volatile.Read/Write calls below.
    int _head; // next position to write
    int _tail; // next position to read

    // Semaphores: count of available items and available slots.
    readonly SemaphoreSlim _itemsAvailable;
    readonly SemaphoreSlim _slotsAvailable;

    /// <summary>
    /// Gets the fixed capacity of the ring buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Returns the number of elements logically stored in the buffer at this instant.
    /// </summary>
    /// <remarks>
    /// Value is approximate under concurrency and should be used for diagnostics only.
    /// </remarks>
    public int Count
    {
        get
        {
            // Cast to uint to get modulo-2^32 difference semantics.
            var head = Volatile.Read(ref _head);
            var tail = Volatile.Read(ref _tail);
            return (int)(uint)(head - tail);
        }
    }

    /// <summary>
    /// True if the buffer contains no items at the instant of the check.
    /// </summary>
    public bool IsEmpty => Volatile.Read(ref _head) == Volatile.Read(ref _tail);

    /// <summary>
    /// True if the buffer cannot accept another item at the instant of the check.
    /// </summary>
    public bool IsFull
    {
        get
        {
            var head = Volatile.Read(ref _head);
            var tail = Volatile.Read(ref _tail);
            return (uint)(head - tail) >= (uint)Capacity;
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="NatsMessageRingBuffer{T}"/> with the given capacity.
    /// </summary>
    /// <param name="capacity">The number of elements the buffer can hold. Must be a power of two (e.g., 1024).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="capacity"/> is less than 2.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="capacity"/> is not a power of two.</exception>
    public NatsActorMessageRingBuffer(int capacity)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be >= 2.");
        if ((capacity & capacity - 1) != 0) throw new ArgumentException("Capacity must be a power of two.", nameof(capacity));

        _buffer = new NatsMsg<byte[]>[capacity];
        _mask = capacity - 1;
        _head = 0;
        _tail = 0;
        _itemsAvailable = new SemaphoreSlim(0); // initially empty
        _slotsAvailable = new SemaphoreSlim(capacity); // all slots free
    }

    /// <summary>
    /// Attempts to enqueue a message into the buffer, blocking if the buffer is full until a slot becomes available.
    /// </summary>
    /// <remarks>This method ensures thread-safe enqueuing of messages into a bounded buffer. If the buffer is
    /// full, the method blocks until a slot becomes available or the operation is canceled via the provided <paramref
    /// name="cancellationToken"/>. The method uses volatile operations to ensure proper memory visibility for
    /// concurrent producers and consumers.</remarks>
    /// <param name="message">The message to enqueue, represented as a <see cref="NatsMsg{T}"/> containing a byte array.</param>
    /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> that can be used to cancel the operation while waiting for a free
    /// slot.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TryEnqueue(in NatsMsg<byte[]> message, CancellationToken? cancellationToken) 
    {
        // block if buffer is full until there is at least one free slot...
        if (IsFull)
        {
            _slotsAvailable.Wait(cancellationToken!.Value);
        }

        // write message first to slot...
        var head = _head;
        _buffer[head & _mask] = message;

        // publish by advancing head and wrap around if needed...
        if (++head == Capacity)
            head = 0;
        Volatile.Write(ref _head, head);

        // signal that a new item is available...
        _itemsAvailable.Release();
    }

    /// <summary>
    /// Attempts to dequeue a message from the buffer, blocking if the buffer is empty until a message is available.
    /// </summary>
    /// <remarks>This method blocks the calling thread if the buffer is empty, waiting until a message is
    /// available to dequeue. It is designed to be used in a consumer-producer scenario where the buffer may be empty at
    /// times.</remarks>
    /// <param name="message">When this method returns, contains the dequeued message if the operation was successful.</param>
    /// <param name="cancellationToken">An optional token to observe while waiting for a message to become available. If the token is canceled, the wait
    /// is aborted.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void TryDequeue(out NatsMsg<byte[]> message, CancellationToken? cancellationToken)
    {
        // block if buffer empty until there is at least one message available...
        _itemsAvailable.Wait(cancellationToken!.Value);

        // consumer-only variable; normal read is fine...
        var tail = _tail;

        // dequeue message...
        var idx = tail & _mask;
        message = _buffer[idx];

        // clear reference to avoid retention (no allocation)...
        _buffer[idx] = default!;

        // publish consumption (release)...
        Volatile.Write(ref _tail, tail + 1);

        // wrap around if needed...
        if (_tail == Capacity)
            Volatile.Write(ref _tail, 0);

        // signal that a slot is now available...
        _slotsAvailable.Release();
    }

    /// <summary>
    /// Removes all items currently stored by repeatedly dequeuing.
    /// </summary>
    /// <remarks>
    /// Safe to call only from the consumer side.
    /// </remarks>
    public void Drain()
    {
        while (!IsEmpty) {
            TryDequeue(out _,default!);
        }
    }

    /// <summary>
    /// Efficiently clears the buffer by consuming all items. Consumer-only.
    /// </summary>
    public void Clear() => Drain();

    /// <summary>
    /// Enqueues an item, spinning until space is available. Producer-only.
    /// </summary>
    /// <param name="item">The item to enqueue.</param>
    /// <remarks>
    /// Use with caution in thread-pool contexts to avoid blocking valuable threads.
    /// </remarks>
    public void EnqueueSpin(in NatsMsg<byte[]> item)
    {
        //while (!TryEnqueue(item))
        {
            Thread.SpinWait(1);
        }
    }

    /// <summary>
    /// Dequeues an item, spinning until one is available. Consumer-only.
    /// </summary>
    /// <returns>The dequeued item.</returns>
    public NatsMsg<byte[]> DequeueSpin()
    {
        NatsMsg<byte[]> item;
        //while (!TryDequeue(out item))
        {
            Thread.SpinWait(1);
        }
        return default!;
    }

   
}
