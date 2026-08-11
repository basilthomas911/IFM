using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.EventChannel;

/// <summary>
/// Describes the operating state of a bounded, lossless, ordered batch channel.
/// </summary>
public readonly record struct OrderedBatchChannelMetrics(
    long AcceptedCount,
    long ProcessedCount,
    long BatchCount,
    long BackpressuredWriteCount,
    long FailureCount,
    double AcceptedPerSecond,
    int Capacity,
    TimeSpan LastQueueDelay,
    TimeSpan MaximumQueueDelay,
    TimeSpan LastBatchDuration,
    TimeSpan MaximumBatchDuration,
    bool IsOpen);

/// <summary>
/// Processes lossless events in arrival order through a bounded asynchronous channel.
/// </summary>
/// <remarks>
/// Writers asynchronously wait when capacity is exhausted. The single reader drains available values into ordered
/// batches, retries a failed batch without reordering it, and faults visibly when the retry limit is exhausted.
/// Normal shutdown completes the writer and drains accepted values before returning.
/// </remarks>
public sealed class OrderedBatchAsyncChannel<T> : IAsyncDisposable
{
    readonly record struct QueuedValue(T Value, long EnqueuedTimestamp);

    readonly Channel<QueuedValue> _channel;
    readonly Func<IReadOnlyList<T>, CancellationToken, ValueTask> _reader;
    readonly Action<OrderedBatchChannelMetrics>? _metricsChanged;
    readonly ILogger? _logger;
    readonly TimeProvider _timeProvider;
    readonly CancellationTokenSource _cancelSource = new();
    readonly Task _processingTask;
    readonly int _capacity;
    readonly int _maximumBatchSize;
    readonly int _readerRetryCount;
    readonly TimeSpan _readerRetryDelay;
    readonly object _stopGate = new();
    readonly long _startedTimestamp;
    long _acceptedCount;
    long _processedCount;
    long _batchCount;
    long _backpressuredWriteCount;
    long _failureCount;
    long _lastQueueDelayTicks;
    long _maximumQueueDelayTicks;
    long _lastBatchDurationTicks;
    long _maximumBatchDurationTicks;
    int _acceptingWrites = 1;
    int _cancelSourceDisposed;

    /// <summary>
    /// Creates and starts a bounded ordered channel.
    /// </summary>
    public OrderedBatchAsyncChannel(
        Func<IReadOnlyList<T>, CancellationToken, ValueTask> reader,
        int capacity = 256,
        int maximumBatchSize = 32,
        int readerRetryCount = 3,
        TimeSpan readerRetryDelay = default,
        ILogger? logger = null,
        TimeProvider? timeProvider = null,
        Action<OrderedBatchChannelMetrics>? metricsChanged = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        if (maximumBatchSize <= 0 || maximumBatchSize > capacity)
            throw new ArgumentOutOfRangeException(nameof(maximumBatchSize), maximumBatchSize, "Batch size must be positive and no greater than capacity.");
        if (readerRetryCount < 0)
            throw new ArgumentOutOfRangeException(nameof(readerRetryCount), readerRetryCount, "Retry count cannot be negative.");
        if (readerRetryDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(readerRetryDelay), readerRetryDelay, "Retry delay cannot be negative.");

        _reader = reader;
        _capacity = capacity;
        _maximumBatchSize = maximumBatchSize;
        _readerRetryCount = readerRetryCount;
        _readerRetryDelay = readerRetryDelay;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _metricsChanged = metricsChanged;
        _startedTimestamp = _timeProvider.GetTimestamp();
        _channel = Channel.CreateBounded<QueuedValue>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
        _processingTask = ProcessAsync(_cancelSource.Token);
    }

    /// <summary>
    /// Gets whether new values are accepted.
    /// </summary>
    public bool IsOpen => Volatile.Read(ref _acceptingWrites) == 1;

    /// <summary>
    /// Gets a thread-safe channel metrics snapshot.
    /// </summary>
    public OrderedBatchChannelMetrics Metrics
    {
        get
        {
            var acceptedCount = Interlocked.Read(ref _acceptedCount);
            var elapsed = _timeProvider.GetElapsedTime(_startedTimestamp, _timeProvider.GetTimestamp());
            return new OrderedBatchChannelMetrics(
                acceptedCount,
                Interlocked.Read(ref _processedCount),
                Interlocked.Read(ref _batchCount),
                Interlocked.Read(ref _backpressuredWriteCount),
                Interlocked.Read(ref _failureCount),
                elapsed > TimeSpan.Zero ? acceptedCount / elapsed.TotalSeconds : 0d,
                _capacity,
                TimeSpan.FromTicks(Interlocked.Read(ref _lastQueueDelayTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _maximumQueueDelayTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _lastBatchDurationTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _maximumBatchDurationTicks)),
                IsOpen);
        }
    }

    /// <summary>
    /// Writes a value, asynchronously applying backpressure when the bounded channel is full.
    /// </summary>
    /// <exception cref="ChannelClosedException">Thrown after shutdown or after an unrecoverable reader failure.</exception>
    public async ValueTask WriteAsync(T value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsOpen)
            throw new ChannelClosedException();

        Interlocked.Increment(ref _acceptedCount);
        try
        {
            var queuedValue = new QueuedValue(value, _timeProvider.GetTimestamp());
            if (!_channel.Writer.TryWrite(queuedValue))
            {
                Interlocked.Increment(ref _backpressuredWriteCount);
                await _channel.Writer.WriteAsync(queuedValue, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            Interlocked.Decrement(ref _acceptedCount);
            throw;
        }
    }

    /// <summary>
    /// Stops accepting writes, drains all accepted values in order, and waits for processing to complete.
    /// </summary>
    public async ValueTask StopAsync()
    {
        lock (_stopGate)
        {
            if (Interlocked.Exchange(ref _acceptingWrites, 0) == 1)
                _channel.Writer.TryComplete();
        }

        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        finally
        {
            DisposeCancellationSource();
            PublishMetrics();
        }
    }

    /// <summary>
    /// Stops immediately and discards values that have not started processing.
    /// </summary>
    public async ValueTask CancelAsync()
    {
        lock (_stopGate)
        {
            if (Interlocked.Exchange(ref _acceptingWrites, 0) == 1)
                _channel.Writer.TryComplete();
            _cancelSource.Cancel();
        }

        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cancelSource.IsCancellationRequested)
        {
        }
        finally
        {
            DisposeCancellationSource();
            PublishMetrics();
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => StopAsync();

    async Task ProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var batch = new List<QueuedValue>(_maximumBatchSize);
                while (batch.Count < _maximumBatchSize && _channel.Reader.TryRead(out var value))
                    batch.Add(value);
                if (batch.Count == 0)
                    continue;

                var processingStarted = _timeProvider.GetTimestamp();
                foreach (var value in batch)
                {
                    var queueDelay = _timeProvider.GetElapsedTime(value.EnqueuedTimestamp, processingStarted);
                    Interlocked.Exchange(ref _lastQueueDelayTicks, queueDelay.Ticks);
                    UpdateMaximum(ref _maximumQueueDelayTicks, queueDelay.Ticks);
                }

                await ProcessBatchWithRetryAsync(batch, cancellationToken).ConfigureAwait(false);
                Interlocked.Add(ref _processedCount, batch.Count);
                Interlocked.Increment(ref _batchCount);
                var batchDuration = _timeProvider.GetElapsedTime(processingStarted, _timeProvider.GetTimestamp());
                Interlocked.Exchange(ref _lastBatchDurationTicks, batchDuration.Ticks);
                UpdateMaximum(ref _maximumBatchDurationTicks, batchDuration.Ticks);
                PublishMetrics();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref _acceptingWrites, 0);
            _channel.Writer.TryComplete(exception);
            _logger?.LogError(exception, "Ordered batch channel failed for {ValueType}", typeof(T).Name);
            throw;
        }
    }

    async ValueTask ProcessBatchWithRetryAsync(
        IReadOnlyList<QueuedValue> batch,
        CancellationToken cancellationToken)
    {
        var values = batch.Select(value => value.Value).ToArray();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await _reader(values, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < _readerRetryCount)
            {
                Interlocked.Increment(ref _failureCount);
                _logger?.LogWarning(
                    exception,
                    "Ordered batch channel reader failed for {ValueType}; retry {Retry}/{MaximumRetries}",
                    typeof(T).Name,
                    attempt + 1,
                    _readerRetryCount);
                if (_readerRetryDelay > TimeSpan.Zero)
                    await Task.Delay(_readerRetryDelay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                Interlocked.Increment(ref _failureCount);
                throw;
            }
        }
    }

    void PublishMetrics()
    {
        try
        {
            _metricsChanged?.Invoke(Metrics);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "Ordered batch channel metrics observer failed for {ValueType}", typeof(T).Name);
        }
    }

    void DisposeCancellationSource()
    {
        if (Interlocked.Exchange(ref _cancelSourceDisposed, 1) == 0)
            _cancelSource.Dispose();
    }

    static void UpdateMaximum(ref long location, long value)
    {
        var current = Interlocked.Read(ref location);
        while (current < value)
        {
            var observed = Interlocked.CompareExchange(ref location, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }
}
