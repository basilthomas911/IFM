using NATS.Client.Core;
using System.Runtime.CompilerServices;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// High-performance, zero-allocation actor thread queue backed by <see cref="BlockingSpscRingBuffer{TMessage}"/>
/// instead of <see cref="System.Threading.Channels.Channel{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// This implementation is designed for the SPSC (single-producer, single-consumer) pattern
/// used by <see cref="ActorThreadV2"/>. It eliminates the per-enqueue allocations that
/// <see cref="System.Threading.Channels.BoundedChannel{T}"/> incurs for async continuations
/// and uses a spin-then-park wait strategy for lower latency under contention.
/// </para>
/// <para>
/// For multi-producer scenarios, use <see cref="ActorThreadQueue"/> (Channel-based) instead.
/// </para>
/// </remarks>
public sealed class ActorThreadQueueV2 : IActorThreadQueue, IDisposable
{
    const int DefaultCapacity = 8192;      // power of two, matches ActorThreadQueue
    const int DefaultSpinEnqueue = 32;
    const int DefaultSpinDequeue = 32;

    int _capacity;
    int _spinEnqueue;
    int _spinDequeue;

    volatile bool _started;
    bool _disposed;
    ActorThreadId _id;
    BlockingSpscRingBuffer<NatsMsg<byte[]>>? _buffer;
    readonly object _startLock = new();

    public ActorThreadQueueV2(int capacity = DefaultCapacity, int spinEnqueue = DefaultSpinEnqueue, int spinDequeue = DefaultSpinDequeue)
    {
        _capacity = capacity;
        _spinEnqueue = spinEnqueue;
        _spinDequeue = spinDequeue;
    }
    /// <inheritdoc />
    public ActorThreadId Id => _id;

    /// <inheritdoc />
    public int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _buffer?.Count ?? 0;
    }

    /// <summary>
    /// Indicates whether this thread queue has been started.
    /// </summary>
    public bool IsStarted
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _started;
    }

    /// <inheritdoc />
    public IActorThreadQueue SetId(ActorThreadId id)
    {
        _id = IsArgumentNull.Set(id);
        return this;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<NatsMsg<byte[]>> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buf = _buffer;
        if (buf is null)
            yield break;

        // Blocking Dequeue runs on the caller's thread (the consumer thread in SPSC).
        // Yielding with Task.Yield keeps the async enumerable cooperative.
        while (!cancellationToken.IsCancellationRequested)
        {
            NatsMsg<byte[]> item;
            try
            {
                item = buf.Dequeue(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (ObjectDisposedException)
            {
                yield break;
            }

            yield return item;

            // Allow the async state machine to observe cancellation between items.
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public IEnumerable<NatsMsg<byte[]>> ReadAll(CancellationToken cancellationToken = default)
    {
        var buf = _buffer;
        if (buf is null)
            yield break;

        while (buf.TryDequeue(out var item))
        {
            yield return item;
        }
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Write(in NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
    { 
        var result = false;
        if (_buffer is not null)
        {
            _buffer.Enqueue(message, cancellationToken);
            result = true;
        }
        return result;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask EnqueueAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
    {
        _buffer!.Enqueue(message, cancellationToken);
        return default;
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Start()
    {
        if (!_started)
            StartCore();
    }

    /// <summary>
    /// Slow-path for <see cref="Start"/>: acquires the lock and creates the SPSC ring buffer.
    /// Kept out-of-line so the JIT can inline the fast path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void StartCore()
    {
        lock (_startLock)
        {
            if (!_started)
            {
                var buf = new BlockingSpscRingBuffer<NatsMsg<byte[]>>(
                    _capacity,
                    _spinEnqueue,
                    _spinDequeue);
                buf.Start();
                _buffer = buf;
                _started = true;
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_buffer is null || !_started)
            return;
        _started = false;
        _buffer.Stop();
        _buffer = null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_started)
            Stop();
    }
}
