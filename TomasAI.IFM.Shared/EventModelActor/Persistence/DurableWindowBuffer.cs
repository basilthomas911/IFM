using System.Threading.Channels;

namespace TomasAI.IFM.Shared.EventModelActor.Persistence;

/// <summary>
/// Event-driven bounded write window. Its deadline is created from the first item and cannot be extended by later arrivals.
/// </summary>
public sealed class DurableWindowBuffer<T> : IAsyncDisposable
{
    readonly Channel<Submission> _channel;
    readonly Func<IReadOnlyList<T>, CancellationToken, ValueTask> _commit;
    readonly int _maximumItems;
    readonly int _maximumBytes;
    readonly TimeSpan _maximumAge;
    readonly TimeSpan _shutdownTimeout;
    readonly CancellationTokenSource _lifetime = new();
    readonly Task _consumer;
    int _disposed;

    public DurableWindowBuffer(
        int capacity,
        int maximumItems,
        int maximumBytes,
        TimeSpan maximumAge,
        TimeSpan shutdownTimeout,
        Func<IReadOnlyList<T>, CancellationToken, ValueTask> commit)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumItems);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        if (maximumAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumAge));
        if (shutdownTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(shutdownTimeout));
        _commit = commit ?? throw new ArgumentNullException(nameof(commit));
        _maximumItems = maximumItems;
        _maximumBytes = maximumBytes;
        _maximumAge = maximumAge;
        _shutdownTimeout = shutdownTimeout;
        _channel = Channel.CreateBounded<Submission>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
        _consumer = ConsumeAsync(_lifetime.Token);
    }

    public async ValueTask SubmitAsync(T item, int sizeBytes, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        if (sizeBytes > _maximumBytes)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "One item exceeds the window byte limit.");
        var submission = Submission.Item(item, sizeBytes);
        await _channel.Writer.WriteAsync(submission, cancellationToken).ConfigureAwait(false);
        await submission.Completion.Task.ConfigureAwait(false);
    }

    /// <summary>Commits every item admitted before the barrier and waits for its terminal outcome.</summary>
    public async ValueTask BarrierAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var barrier = Submission.Barrier();
        await _channel.Writer.WriteAsync(barrier, cancellationToken).ConfigureAwait(false);
        await barrier.Completion.Task.ConfigureAwait(false);
    }

    async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        Submission? carry = null;
        try
        {
            while (carry is not null || await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var first = carry ?? (_channel.Reader.TryRead(out var read) ? read : null);
                carry = null;
                if (first is null) continue;
                if (first.IsBarrier)
                {
                    first.Completion.TrySetResult();
                    continue;
                }

                var batch = new List<Submission>(_maximumItems) { first };
                var bytes = first.SizeBytes;
                using var stopDeadline = new CancellationTokenSource();
                var deadline = Task.Delay(_maximumAge, stopDeadline.Token);
                while (batch.Count < _maximumItems && bytes < _maximumBytes)
                {
                    if (_channel.Reader.TryRead(out var next))
                    {
                        if (next.IsBarrier || bytes + next.SizeBytes > _maximumBytes)
                        {
                            carry = next;
                            break;
                        }
                        batch.Add(next);
                        bytes += next.SizeBytes;
                        continue;
                    }
                    var available = _channel.Reader.WaitToReadAsync(cancellationToken).AsTask();
                    var completed = await Task.WhenAny(available, deadline).ConfigureAwait(false);
                    if (completed == deadline || !await available.ConfigureAwait(false)) break;
                }
                stopDeadline.Cancel();
                await CommitAsync(batch, cancellationToken).ConfigureAwait(false);
                if (carry is { IsBarrier: true })
                {
                    carry.Completion.TrySetResult();
                    carry = null;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            FailRemaining(new OperationCanceledException("Durable window stopped before all admitted work completed."), carry);
        }
        catch (Exception exception)
        {
            FailRemaining(exception, carry);
        }
    }

    async ValueTask CommitAsync(IReadOnlyList<Submission> submissions, CancellationToken cancellationToken)
    {
        try
        {
            var items = new T[submissions.Count];
            for (var index = 0; index < submissions.Count; index++) items[index] = submissions[index].Value!;
            await _commit(items, cancellationToken).ConfigureAwait(false);
            foreach (var submission in submissions) submission.Completion.TrySetResult();
        }
        catch (Exception exception)
        {
            foreach (var submission in submissions) submission.Completion.TrySetException(exception);
        }
    }

    void FailRemaining(Exception exception, Submission? carry)
    {
        carry?.Completion.TrySetException(exception);
        while (_channel.Reader.TryRead(out var pending)) pending.Completion.TrySetException(exception);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _channel.Writer.TryComplete();
        if (await Task.WhenAny(_consumer, Task.Delay(_shutdownTimeout)).ConfigureAwait(false) != _consumer)
            _lifetime.Cancel();
        try { await _consumer.ConfigureAwait(false); } catch (OperationCanceledException) { }
        _lifetime.Dispose();
    }

    sealed class Submission
    {
        Submission(T? value, int sizeBytes, bool isBarrier)
        {
            Value = value;
            SizeBytes = sizeBytes;
            IsBarrier = isBarrier;
        }
        public T? Value { get; }
        public int SizeBytes { get; }
        public bool IsBarrier { get; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public static Submission Item(T value, int sizeBytes) => new(value, sizeBytes, false);
        public static Submission Barrier() => new(default, 0, true);
    }
}
