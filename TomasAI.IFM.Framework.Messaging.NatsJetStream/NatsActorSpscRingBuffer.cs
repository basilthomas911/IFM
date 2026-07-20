using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Framework.Messaging.Nats;

/// <summary>
/// High-performance Single-Producer / Single-Consumer (SPSC) blocking ring buffer backed by <see cref="ArrayPool{T}"/>.
/// 
/// Uses a hybrid wait strategy for "block when full/empty":
///   1) Spin briefly in user-mode for the fast path.
///   2) Park (OS wait) via <see cref="AutoResetEvent"/> only if contention persists.
/// 
/// Cancellation is supported without per-operation heap allocations (no per-wait arrays).
/// Capacity is a power of two for bitmask wrap, and indices are cache-line padded to reduce false sharing.
/// </summary>
/// <remarks>
/// <para>Threading model: exactly one producer thread and one consumer thread concurrently (SPSC).</para>
/// <para>After construction, steady-state Enqueue/Dequeue do not allocate.</para>
/// <para>
/// Indexing scheme: <c>_head</c> and <c>_tail</c> are monotonically increasing sequence numbers.
/// Slot index = <c>sequence &amp; _mask</c>.
/// Count = <c>(uint)(_head - _tail)</c>.
/// Full when count == capacity. Empty when count == 0.
/// This avoids the classic "lose one slot" problem of masked-index ring buffers.
/// </para>
/// </remarks>
public sealed class NatsActorSpscRingBuffer : IActorSpscRingBuffer<NatsMsg<byte[]>>
{
    // === Cache-line isolation to avoid false sharing ===
    // Each PaddedInt occupies 128 bytes with Value centered at offset 64,
    // ensuring isolation on both 64-byte (x86) and 128-byte (ARM/Apple M) cache lines.
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    struct PaddedInt
    {
        [FieldOffset(64)] public int Value;
    }

    // rented backing storage (may be larger than logical capacity)
    ArrayPool<NatsMsg<byte[]>> _pool = default!;
    NatsMsg<byte[]>[] _buffer = default!;
    readonly int _capacity;      // logical capacity (power of two)
    readonly int _mask;          // = _capacity - 1

    // SPSC monotonic indices on isolated cache lines.
    // _head: written by producer, read by consumer.
    // _tail: written by consumer, read by producer.
    PaddedInt _head;
    PaddedInt _tail;

    // Transition-notification events
    readonly AutoResetEvent _itemAvailable;  // empty -> non-empty
    readonly AutoResetEvent _slotAvailable;  // full  -> non-full

    // Cancellation events to avoid per-wait allocations
    readonly AutoResetEvent _producerCancel;
    readonly AutoResetEvent _consumerCancel;

    // Cached handle arrays (no per-wait allocation)
    readonly WaitHandle[] _waitItemOrCancel;
    readonly WaitHandle[] _waitSlotOrCancel;

    // Spin budgets (tune per workload)
    readonly int _spinCountEnqueue;
    readonly int _spinCountDequeue;

    bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="NatsActorSpscRingBuffer"/> class with the specified capacity and
    /// spin counts.
    /// </summary>
    /// <param name="capacityPowerOfTwo">The capacity of the buffer, which must be a power of two and greater than or equal to 1.</param>
    /// <param name="spinCountEnqueue">The number of spin-wait iterations to perform when attempting to enqueue an item.</param>
    /// <param name="spinCountDequeue">The number of spin-wait iterations to perform when attempting to dequeue an item.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="capacityPowerOfTwo"/> is not a power of two or is less than 1.</exception>
    public NatsActorSpscRingBuffer(
        int capacityPowerOfTwo,
        int spinCountEnqueue,
        int spinCountDequeue)
    {
        if (capacityPowerOfTwo < 1 || (capacityPowerOfTwo & capacityPowerOfTwo - 1) != 0)
            throw new ArgumentOutOfRangeException(nameof(capacityPowerOfTwo), "NatsActorSpscRingBuffer: Capacity must be a power of two and >= 1.");

        _capacity = capacityPowerOfTwo;
        _mask = _capacity - 1;
        _head.Value = 0;
        _tail.Value = 0;

        _itemAvailable = new AutoResetEvent(false);
        _slotAvailable = new AutoResetEvent(false);

        _producerCancel = new AutoResetEvent(false);
        _consumerCancel = new AutoResetEvent(false);

        _waitItemOrCancel = [_itemAvailable, _consumerCancel];
        _waitSlotOrCancel = [_slotAvailable, _producerCancel];

        _spinCountEnqueue = spinCountEnqueue;
        _spinCountDequeue = spinCountDequeue;
    }

    /// <summary>Logical capacity of the ring buffer (power of two).</summary>
    public int Capacity => _capacity;

    /// <summary>Approximate count computed from monotonic indices (diagnostic only).</summary>
    public int Count
    {
        get
        {
            // Monotonic indices: unsigned subtraction handles 32-bit wrap correctly.
            var head = Volatile.Read(ref _head.Value);
            var tail = Volatile.Read(ref _tail.Value);
            return (int)((uint)head - (uint)tail);
        }
    }


    /// <summary>
    /// Initializes the internal resources required for the operation.
    /// </summary>
    /// <remarks>This method prepares the instance for use by allocating a buffer from a shared array pool. It
    /// must be called before invoking any operations that depend on the allocated resources.</remarks>
    public void Start()
    {
        ThrowIfDisposed();
        _pool = ArrayPool<NatsMsg<byte[]>>.Shared;
        _buffer = _pool.Rent(_capacity);
    }

    /// <summary>
    /// Stops the operation and releases all associated resources.
    /// </summary>
    /// <remarks>This method ensures that all resources, such as buffers and synchronization primitives, are
    /// properly disposed. Once called, the instance cannot be reused.</remarks>
    public void Stop()
    {
        if (_disposed || _pool is null || _buffer is null)
            return;
        _disposed = true;

        _pool.Return(_buffer, clearArray: true);
        _buffer = [];
        _itemAvailable.Dispose();
        _slotAvailable.Dispose();
        _producerCancel.Dispose();
        _consumerCancel.Dispose();
    }

    /// <summary>
    /// Enqueues an item, blocking (cancellable) when the buffer is full. Uses spin-then-park strategy.
    /// </summary>
    public void Enqueue(in NatsMsg<byte[]> item, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Fast path: spin a little while trying to enqueue
        var spinner = new SpinWait();
        for (int i = 0; i < _spinCountEnqueue; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryEnqueue(item)) 
                return;
            spinner.SpinOnce();
        }

        // Slow path: park until a slot becomes available or cancellation occurs
        BlockUntilSlotAvailable(cancellationToken);

        // After being signaled, retry until success (usually succeeds immediately)
        while (!TryEnqueue(item))
        {
            // If a spurious wake or race occurred, re-park
            BlockUntilSlotAvailable(cancellationToken);
        }
    }

    
    /// <summary>
    /// Dequeues and returns the next item, blocking (cancellable) when the buffer is empty. Uses spin-then-park.
    /// </summary>
    public NatsMsg<byte[]>  Dequeue(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        // Fast path: spin a little while trying to dequeue
        var spinner = new SpinWait();
        for (int i = 0; i < _spinCountDequeue; i++)
        {
            if (TryDequeue(out var value)) 
                return value;
            spinner.SpinOnce();
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Slow path: park until an item is available or cancellation occurs
        BlockUntilItemAvailable(cancellationToken);

        while (true)
        {
            if (TryDequeue(out var value)) 
                return value;
            BlockUntilItemAvailable(cancellationToken); // spurious wake; re-park
        }
    }

    /// <summary>
    /// Non-blocking attempt to enqueue a value. Returns false if the buffer is full.
    /// Called only by the producer thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryEnqueue(NatsMsg<byte[]> item)
    {
        // Producer owns _head — plain read is safe (only this thread writes it).
        int head = _head.Value;
        // Must volatile-read _tail (written by consumer on another thread).
        int tail = Volatile.Read(ref _tail.Value);

        // Full when all slots are occupied (monotonic difference == capacity).
        if ((uint)(head - tail) >= (uint)_capacity)
            return false;

        // Snapshot empty state before publishing, using the same head/tail values.
        bool wasEmpty = head == tail;

        // Store item at the masked slot index.
        _buffer[head & _mask] = item;

        // Publish the new head with release semantics so the consumer
        // sees the stored item before the updated index.
        Volatile.Write(ref _head.Value, head + 1);

        // Signal consumer only on empty → non-empty transition.
        if (wasEmpty)
            _itemAvailable.Set();

        return true;
    }

    /// <summary>
    /// Non-blocking attempt to dequeue a value. Returns false if the buffer is empty.
    /// Called only by the consumer thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool TryDequeue(out NatsMsg<byte[]> item)
    {
        // Consumer owns _tail — plain read is safe (only this thread writes it).
        int tail = _tail.Value;
        // Must volatile-read _head (written by producer on another thread).
        int head = Volatile.Read(ref _head.Value);

        if (head == tail)
        {
            item = default;
            return false;
        }

        // Snapshot full state before publishing, using the same head/tail values.
        bool wasFull = (uint)(head - tail) >= (uint)_capacity;

        int slot = tail & _mask;
        item = _buffer[slot];

        // Clear the slot to avoid retaining reference-type fields inside the struct.
        _buffer[slot] = default;

        // Publish the new tail with release semantics.
        Volatile.Write(ref _tail.Value, tail + 1);

        // Signal producer only on full → non-full transition.
        if (wasFull)
            _slotAvailable.Set();

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(NatsActorSpscRingBuffer));
    }

    // === Blocking helpers (no per-wait allocation) ===
    void BlockUntilSlotAvailable(CancellationToken ct)
    {
        using var reg = ct.CanBeCanceled
            ? ct.Register(static s => ((AutoResetEvent)s!).Set(), _producerCancel)
            : default;

        int signaled = WaitHandle.WaitAny(_waitSlotOrCancel); // 0 = slot available, 1 = cancel
        if (signaled == 1) throw new OperationCanceledException(ct);
    }

    void BlockUntilItemAvailable(CancellationToken ct)
    {
        using var reg = ct.CanBeCanceled
            ? ct.Register(static s => ((AutoResetEvent)s!).Set(), _consumerCancel)
            : default;

        int signaled = WaitHandle.WaitAny(_waitItemOrCancel); // 0 = item available, 1 = cancel
        if (signaled == 1) throw new OperationCanceledException(ct);
    }
}
