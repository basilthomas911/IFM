using NATS.Client.Core;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>
/// Provides a thread-safe queue for managing messages in an actor model, allowing for asynchronous message processing
/// and communication.
/// </summary>
/// <remarks>This class implements the IActorThreadQueue interface and IDisposable, ensuring proper resource
/// management. It must be initialized before any message operations can be performed, and it supports both synchronous
/// and asynchronous message reading and writing.</remarks>
public sealed class ActorThreadQueue()
    : IActorThreadQueue, IDisposable
{
    volatile bool _started = false;
    bool _disposed = false;
    ActorThreadId _id;
    Channel<NatsMsg<byte[]>> _messageChannel;
    readonly object _startedLock = new();

    /// <summary>
    /// Gets the unique identifier for the actor thread.
    /// </summary>
    public ActorThreadId Id => _id;

    /// <summary>
    /// Gets the number of messages currently available to be read from the message channel.
    /// </summary>
    /// <remarks>If the message channel has not been initialized, this property returns 0. The value reflects
    /// the count of messages that can be read without waiting.</remarks>
    public int Count { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _messageChannel?.Reader.Count ?? 0; }

    /// <summary>
    /// Indicates whether this thread queue has been started.
    /// </summary>
    public bool IsStarted { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => _started; }

    /// <summary>
    /// Sets the identifier for the actor thread queue.
    /// </summary>
    /// <param name="id">The <see cref="ActorThreadId"/> to assign to the actor thread queue. Cannot be <see langword="null"/>.</param>
    /// <returns>The current instance of <see cref="IActorThreadQueue"/> to allow method chaining.</returns>
    public IActorThreadQueue SetId(ActorThreadId id)
    {
        _id = IsArgumentNull.Set(id);
        return this;
    }

    /// <summary>
    /// Asynchronously reads all available messages from the message channel and yields them as they become available.
    /// </summary>
    /// <remarks>The method yields messages until the channel is empty or cancellation is requested. The
    /// message channel must be initialized before calling this method.</remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous read operation.</param>
    /// <returns>An asynchronous stream of NatsMsg<byte[]> objects representing the messages read from the channel.</returns>
    public async IAsyncEnumerable<NatsMsg<byte[]>> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = _messageChannel;
        if (channel is null) 
            yield break;

        var reader = channel.Reader;
        await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
        if (cancellationToken.IsCancellationRequested) 
            yield break;
        while (reader.TryRead(out var item))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Reads all available messages from the channel and returns them as an enumerable collection.
    /// </summary>
    /// <remarks>This method yields messages that are immediately available in the channel. It does not wait
    /// for new messages to arrive. The operation can be cancelled by providing a cancellation token.</remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the read operation. If not specified, the operation will not be
    /// cancelled.</param>
    /// <returns>An enumerable collection of messages read from the channel. The collection may be empty if no messages are
    /// available.</returns>
    public IEnumerable<NatsMsg<byte[]>> ReadAll(CancellationToken cancellationToken = default)
    {
        while (_messageChannel is not null && _messageChannel.Reader.TryRead(out NatsMsg<byte[]> item))
        {
            yield return item;
        }
    }


    /// <summary>
    /// Attempts to write the specified message to the underlying message channel.
    /// </summary>
    /// <remarks>This method uses a non-blocking write operation. If the channel is full or unavailable, the
    /// method returns <see langword="false"/>.</remarks>
    /// <param name="message">The message to be written. This parameter is passed by reference for performance reasons.</param>
    /// <returns><see langword="true"/> if the message was successfully written to the channel; otherwise, <see
    /// langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Write(in NatsMsg<byte[]> message)
        =>_messageChannel.Writer.TryWrite(message);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Write(in NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
        => _messageChannel.Writer.TryWrite(message);

    /// <summary>
    /// Asynchronously enqueues a message to the channel, waiting for space to become available if the channel is full.
    /// </summary>
    /// <remarks>The caller must ensure <see cref="Start"/> has been called before invoking this method.
    /// On the hot path (via <see cref="ActorThreadQueues"/>), the queue is always started by <see cref="GetThreadQueue"/>
    /// before this method is reached, making a redundant <see cref="Start"/> check unnecessary.</remarks>
    /// <param name="message">The message to enqueue, represented as a <see cref="NatsMsg{T}"/> containing a byte array payload.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the enqueue operation.</param>
    /// <returns>A task that represents the asynchronous enqueue operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask EnqueueAsync(NatsMsg<byte[]> message, CancellationToken cancellationToken = default)
        => _messageChannel.Writer.WriteAsync(message, cancellationToken);

    /// <summary>
    /// Initializes the message channel for communication. This method must be called before any message operations can
    /// be performed.
    /// </summary>
    /// <remarks>This method is thread-safe and ensures that the message channel is created only once, even
    /// when called from multiple threads. It uses a bounded channel with a capacity of 8192 messages, allowing for
    /// single writer and single reader scenarios. The fast-path check avoids entering the lock when already started.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Start()
    {
        if (!_started)
            StartCore();
    }

    /// <summary>
    /// Slow-path for <see cref="Start"/>: acquires the lock and creates the bounded channel.
    /// Kept out-of-line so the JIT can inline the fast path.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    void StartCore()
    {
        lock (_startedLock)
        {
            if (!_started)
            {
                _messageChannel = Channel.CreateBounded<NatsMsg<byte[]>>(new BoundedChannelOptions(8192)
                {
                    SingleWriter = false,
                    SingleReader = true
                });
                _started = true;
            }
        }
    }

    /// <summary>
    /// Stops the message processing operation and releases associated resources.
    /// </summary>
    /// <remarks>This method marks the message channel as complete, preventing further messages from being
    /// written.  After calling this method, the instance is no longer in a started state and cannot be
    /// restarted.</remarks>
    public void Stop()
    {
        if(_messageChannel is null || !_started) 
            return;
        _messageChannel.Writer.Complete();
        _messageChannel = null!;
        _started = false;
    }

    /// <summary>
    /// Releases all resources used by the actor thread queue, completing the message channel if still active.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_started)
            Stop();
    }
}
