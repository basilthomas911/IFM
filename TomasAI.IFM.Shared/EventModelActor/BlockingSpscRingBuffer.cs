using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

[module: SkipLocalsInit]

namespace TomasAI.IFM.Shared.EventModelActor;

// Cache-line isolation to avoid false sharing.
// Each PaddedInt occupies 128 bytes with Value centered at offset 64,
// ensuring isolation on both 64-byte (x86) and 128-byte (ARM/Apple M) cache lines.
// Defined outside the generic class because generic types cannot have explicit layout.
[StructLayout(LayoutKind.Explicit, Size = 128)]
struct PaddedInt
{
    [FieldOffset(64)] public int Value;
}

/// <summary>
/// High-performance Single-Producer / Single-Consumer (SPSC) blocking ring buffer backed by <see cref="ArrayPool{T}"/>.
/// 
/// Uses a hybrid wait strategy for "block when full/empty":
///   1) Spin briefly in user-mode for the fast path.
///   2) Park via <see cref="ManualResetEventSlim"/> only if contention persists.
/// 
/// Cancellation is handled natively by <see cref="ManualResetEventSlim.Wait(CancellationToken)"/> — no per-wait
/// allocations (no separate cancel events, no CancellationTokenRegistration, no WaitHandle arrays).
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
/// <para>
/// Performance optimisations:
/// <list type="bullet">
///   <item><see cref="ManualResetEventSlim"/> instead of <see cref="AutoResetEvent"/> — user-mode spin before kernel transition.</item>
///   <item><see cref="Unsafe.Add{T}(ref T, int)"/> + <see cref="MemoryMarshal.GetArrayDataReference{T}(T[])"/> — eliminates array bounds checks.</item>
///   <item><see cref="Unsafe.SkipInit{T}(out T)"/> — skips zero-init for out parameters on the fast-fail path.</item>
///   <item><see cref="RuntimeHelpers.IsReferenceOrContainsReferences{T}()"/> — JIT constant-folds slot clearing for pure value types.</item>
///   <item><see cref="SpinWait.SpinOnce(int)"/> with <c>sleep1Threshold: -1</c> — avoids <c>Thread.Sleep(1)</c> which can sleep 15.6 ms on Windows.</item>
///   <item><c>[MethodImpl(NoInlining)]</c> on slow paths keeps the JIT'd fast path compact.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class BlockingSpscRingBuffer<TMessage>
 : ISpscRingBuffer<TMessage>, IDisposable where TMessage : struct
{
    // rented backing storage (may be larger than logical capacity)
    ArrayPool<TMessage> _pool;
    TMessage[] _buffer;
    readonly int _capacity;      // logical capacity (power of two)
    readonly int _mask;          // = _capacity - 1

    // SPSC monotonic indices on isolated cache lines.
    // _head: written by producer, read by consumer.
    // _tail: written by consumer, read by producer.
    PaddedInt _head;
    PaddedInt _tail;

    // Transition-notification events (ManualResetEventSlim: user-mode spin before kernel)
    // _itemAvailable: signalled by producer on empty → non-empty; waited by consumer.
    // _slotAvailable: signalled by consumer on full → non-full; waited by producer.
    readonly ManualResetEventSlim _itemAvailable;
    readonly ManualResetEventSlim _slotAvailable;

    // Spin budgets (tune per workload)
    readonly int _spinCountEnqueue;
    readonly int _spinCountDequeue;

    bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlockingSpscRingBuffer{TMessage}"/> class with the specified capacity and
    /// spin counts.
    /// </summary>
    /// <param name="capacityPowerOfTwo">The capacity of the buffer, which must be a power of two and greater than or equal to 1.</param>
    /// <param name="spinCountEnqueue">The number of spin-wait iterations to perform when attempting to enqueue an item.</param>
    /// <param name="spinCountDequeue">The number of spin-wait iterations to perform when attempting to dequeue an item.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="capacityPowerOfTwo"/> is not a power of two or is less than 1.</exception>
    public BlockingSpscRingBuffer(
        int capacityPowerOfTwo,
        int spinCountEnqueue,
        int spinCountDequeue)
    {
        if (capacityPowerOfTwo < 1 || (capacityPowerOfTwo & (capacityPowerOfTwo - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(capacityPowerOfTwo), "Capacity must be a power of two and >= 1.");

        _capacity = capacityPowerOfTwo;
        _mask = _capacity - 1;
        _head.Value = 0;
        _tail.Value = 0;

        _itemAvailable = new ManualResetEventSlim(false);
        _slotAvailable = new ManualResetEventSlim(false);

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
        if (_disposed) ThrowObjectDisposed();
        _pool = ArrayPool<TMessage>.Shared;
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
    }

    /// <inheritdoc />
    public void Dispose() => Stop();

    /// <summary>
    /// Enqueues an item, blocking (cancellable) when the buffer is full. Uses spin-then-park strategy.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Enqueue(in TMessage item, CancellationToken cancellationToken = default)
    {
        if (_disposed) ThrowObjectDisposed();

        // Optimistic fast path: succeed without spinning when buffer is not full.
        if (TryEnqueue(item))
            return;

        // Contended path: spin then park (kept out-of-line to keep this method compact).
        EnqueueWithSpin(item, cancellationToken);
    }

    /// <summary>
    /// Dequeues and returns the next item, blocking (cancellable) when the buffer is empty. Uses spin-then-park.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TMessage Dequeue(CancellationToken cancellationToken = default)
    {
        if (_disposed) ThrowObjectDisposed();

        // Optimistic fast path: succeed without spinning when buffer is not empty.
        if (TryDequeue(out var value))
            return value;

        // Contended path: spin then park (kept out-of-line to keep this method compact).
        return DequeueWithSpin(cancellationToken);
    }

    // === Spin paths (NoInlining keeps the fast path JIT'd code compact) ===

    [MethodImpl(MethodImplOptions.NoInlining)]
    void EnqueueWithSpin(in TMessage item, CancellationToken cancellationToken)
    {
        // sleep1Threshold: -1 avoids Thread.Sleep(1) which can stall 15.6 ms on Windows.
        var spinner = new SpinWait();
        for (var i = 0; i < _spinCountEnqueue; i++)
        {
            spinner.SpinOnce(sleep1Threshold: -1);
            if (TryEnqueue(item))
                return;
        }

        EnqueueSlow(item, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    TMessage DequeueWithSpin(CancellationToken cancellationToken)
    {
        var spinner = new SpinWait();
        for (var i = 0; i < _spinCountDequeue; i++)
        {
            spinner.SpinOnce(sleep1Threshold: -1);
            if (TryDequeue(out var value))
                return value;
        }

        return DequeueSlow(cancellationToken);
    }

    // === Park paths ===

    /// <summary>
    /// Blocking enqueue using Reset-Check-Wait pattern on <see cref="ManualResetEventSlim"/>.
    /// Cancellation is handled natively by <see cref="ManualResetEventSlim.Wait(CancellationToken)"/>
    /// — no per-wait CancellationTokenRegistration or WaitHandle allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void EnqueueSlow(in TMessage item, CancellationToken cancellationToken)
    {
        while (true)
        {
            // Reset before checking condition to prevent lost wakeups:
            // any Set() between Reset and Wait keeps the event signalled.
            _slotAvailable.Reset();
            if (TryEnqueue(item))
                return;
            _slotAvailable.Wait(cancellationToken); // throws OperationCanceledException
        }
    }

    /// <summary>
    /// Blocking dequeue using Reset-Check-Wait pattern on <see cref="ManualResetEventSlim"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    TMessage DequeueSlow(CancellationToken cancellationToken)
    {
        while (true)
        {
            _itemAvailable.Reset();
            if (TryDequeue(out var value))
                return value;
            _itemAvailable.Wait(cancellationToken); // throws OperationCanceledException
        }
    }

    /// <summary>
    /// Non-blocking attempt to enqueue a value. Returns false if the buffer is full.
    /// Called only by the producer thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryEnqueue(in TMessage item)
    {
        // Producer owns _head — plain read is safe (only this thread writes it).
        var head = _head.Value;

        // Must volatile-read _tail (written by consumer on another thread).
        var tail = Volatile.Read(ref _tail.Value);

        // Full when all slots are occupied (monotonic difference == capacity).
        if ((uint)(head - tail) >= (uint)_capacity)
            return false;

        // Snapshot empty state before publishing, using the same head/tail values.
        var wasEmpty = head == tail;

        // Store item at the masked slot index — Unsafe.Add eliminates bounds check.
        Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_buffer), head & _mask) = item;

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
    public bool TryDequeue(out TMessage item)
    {
        // Consumer owns _tail — plain read is safe (only this thread writes it).
        var tail = _tail.Value;

        // Must volatile-read _head (written by producer on another thread).
        var head = Volatile.Read(ref _head.Value);
        if (head == tail)
        {
            // SkipInit: avoid zero-initialising the out param on the fast-fail path.
            Unsafe.SkipInit(out item);
            return false;
        }

        // Snapshot full state before publishing, using the same head/tail values.
        var wasFull = (uint)(head - tail) >= (uint)_capacity;

        // Bounds-check-free slot access via Unsafe.Add on the array data reference.
        ref TMessage slot = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(_buffer), tail & _mask);
        item = slot;

        // Clear the slot only when TMessage contains reference-type fields.
        // RuntimeHelpers.IsReferenceOrContainsReferences<T>() is a JIT intrinsic constant —
        // for pure value types this entire branch is eliminated at JIT time.
        if (RuntimeHelpers.IsReferenceOrContainsReferences<TMessage>())
            slot = default;

        // Publish the new tail with release semantics.
        Volatile.Write(ref _tail.Value, tail + 1);

        // Signal producer only on full → non-full transition.
        if (wasFull)
            _slotAvailable.Set();

        return true;
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void ThrowObjectDisposed() =>
        throw new ObjectDisposedException(nameof(BlockingSpscRingBuffer<TMessage>));
}
