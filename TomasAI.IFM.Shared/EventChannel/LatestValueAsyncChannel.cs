using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.EventChannel;

/// <summary>
/// Describes the current operating state of a latest-value channel.
/// </summary>
/// <param name="AcceptedCount">The number of values accepted while the channel was open.</param>
/// <param name="ProcessedCount">The number of values whose reader callback completed successfully.</param>
/// <param name="CoalescedCount">The number of pending values superseded by a newer value.</param>
/// <param name="FailureCount">The number of reader callback failures.</param>
/// <param name="AcceptedPerSecond">The average accepted event rate since the channel was created.</param>
/// <param name="LastQueueDelay">The queue delay of the most recently dispatched value.</param>
/// <param name="MaximumQueueDelay">The greatest observed queue delay.</param>
/// <param name="LastProcessingDuration">The processing duration of the most recently completed callback.</param>
/// <param name="MaximumProcessingDuration">The greatest observed callback processing duration.</param>
/// <param name="IsOpen">Whether the channel still accepts writes.</param>
public readonly record struct LatestValueChannelMetrics(
    long AcceptedCount,
    long ProcessedCount,
    long CoalescedCount,
    long FailureCount,
    double AcceptedPerSecond,
    TimeSpan LastQueueDelay,
    TimeSpan MaximumQueueDelay,
    TimeSpan LastProcessingDuration,
    TimeSpan MaximumProcessingDuration,
    bool IsOpen);

/// <summary>
/// Processes replaceable realtime state through a bounded, asynchronous, latest-value channel.
/// </summary>
/// <remarks>
/// <para>
/// The channel accepts concurrent writers and has one reader. Its capacity is one and its full mode is
/// <see cref="BoundedChannelFullMode.DropOldest"/>, so a pending value is replaced when a newer value arrives while
/// the reader is busy. Callbacks are serialized and execute without a dedicated operating-system thread.
/// </para>
/// <para>
/// This type is intended for replaceable display state such as quotes, prices, Greeks, and display-only profit and
/// loss. It must not be used for lossless business events such as orders, fills, alerts, or state transitions.
/// </para>
/// </remarks>
/// <typeparam name="T">The replaceable value type.</typeparam>
public sealed class LatestValueAsyncChannel<T> : IAsyncDisposable
{
    readonly Channel<T> _channel;
    readonly Func<T, CancellationToken, ValueTask> _reader;
    readonly Action<LatestValueChannelMetrics>? _metricsChanged;
    readonly TimeSpan _minimumInterval;
    readonly TimeProvider _timeProvider;
    readonly ILogger? _logger;
    readonly CancellationTokenSource _stopSource = new();
    readonly Task _processingTask;
    readonly object _pendingGate = new();
    readonly long _startedTimestamp;
    bool _hasPendingValue;
    long _pendingTimestamp;
    long _acceptedCount;
    long _processedCount;
    long _coalescedCount;
    long _failureCount;
    long _lastQueueDelayTicks;
    long _maximumQueueDelayTicks;
    long _lastProcessingDurationTicks;
    long _maximumProcessingDurationTicks;
    int _acceptingWrites = 1;
    int _stopSourceDisposed;

    /// <summary>
    /// Creates and starts a latest-value channel.
    /// </summary>
    /// <param name="reader">The serialized asynchronous callback that processes the latest value.</param>
    /// <param name="minimumInterval">The minimum delay after one callback completes before another begins.</param>
    /// <param name="logger">An optional logger for callback failures.</param>
    /// <param name="timeProvider">An optional time provider used for the minimum interval.</param>
    /// <param name="metricsChanged">An optional observer invoked after processing and lifecycle changes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumInterval"/> is negative.</exception>
    public LatestValueAsyncChannel(
        Func<T, CancellationToken, ValueTask> reader,
        TimeSpan minimumInterval = default,
        ILogger? logger = null,
        TimeProvider? timeProvider = null,
        Action<LatestValueChannelMetrics>? metricsChanged = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (minimumInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumInterval), minimumInterval, "The minimum interval cannot be negative.");

        _reader = reader;
        _minimumInterval = minimumInterval;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _metricsChanged = metricsChanged;
        _startedTimestamp = _timeProvider.GetTimestamp();
        _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
            AllowSynchronousContinuations = false
        });
        _processingTask = ProcessAsync(_stopSource.Token);
    }

    /// <summary>
    /// Gets whether the channel is accepting writes.
    /// </summary>
    public bool IsOpen => Volatile.Read(ref _acceptingWrites) == 1;

    /// <summary>
    /// Gets a thread-safe snapshot of event rate, coalescing, queue-delay, processing, and lifecycle metrics.
    /// </summary>
    public LatestValueChannelMetrics Metrics
    {
        get
        {
            var acceptedCount = Interlocked.Read(ref _acceptedCount);
            var elapsed = _timeProvider.GetElapsedTime(_startedTimestamp, _timeProvider.GetTimestamp());
            var acceptedPerSecond = elapsed > TimeSpan.Zero
                ? acceptedCount / elapsed.TotalSeconds
                : 0d;
            return new LatestValueChannelMetrics(
                acceptedCount,
                Interlocked.Read(ref _processedCount),
                Interlocked.Read(ref _coalescedCount),
                Interlocked.Read(ref _failureCount),
                acceptedPerSecond,
                TimeSpan.FromTicks(Interlocked.Read(ref _lastQueueDelayTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _maximumQueueDelayTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _lastProcessingDurationTicks)),
                TimeSpan.FromTicks(Interlocked.Read(ref _maximumProcessingDurationTicks)),
                IsOpen);
        }
    }

    /// <summary>
    /// Attempts to publish a value. A successful write can replace an older pending value.
    /// </summary>
    /// <param name="value">The newest replaceable value.</param>
    /// <returns><see langword="true"/> when accepted; otherwise <see langword="false"/> after shutdown.</returns>
    public bool TryWrite(T value)
    {
        lock (_pendingGate)
        {
            if (!IsOpen)
                return false;

            if (_hasPendingValue)
                Interlocked.Increment(ref _coalescedCount);

            if (!_channel.Writer.TryWrite(value))
                return false;

            _hasPendingValue = true;
            _pendingTimestamp = _timeProvider.GetTimestamp();
            Interlocked.Increment(ref _acceptedCount);
        }

        return true;
    }

    /// <summary>
    /// Stops accepting writes, cancels current processing, and asynchronously waits for the reader loop to finish.
    /// Pending display state is discarded because it is no longer useful after its owner closes.
    /// </summary>
    public async ValueTask StopAsync()
    {
        lock (_pendingGate)
        {
            if (Interlocked.Exchange(ref _acceptingWrites, 0) == 1)
            {
                _hasPendingValue = false;
                _channel.Writer.TryComplete();
                _stopSource.Cancel();
            }
        }

        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        finally
        {
            if (Interlocked.Exchange(ref _stopSourceDisposed, 1) == 0)
                _stopSource.Dispose();
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
                T latestValue;
                long queuedTimestamp;
                lock (_pendingGate)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    if (!_channel.Reader.TryRead(out latestValue!))
                        continue;
                    _hasPendingValue = false;
                    queuedTimestamp = _pendingTimestamp;
                }

                var processingStarted = _timeProvider.GetTimestamp();
                var queueDelay = _timeProvider.GetElapsedTime(queuedTimestamp, processingStarted);
                Interlocked.Exchange(ref _lastQueueDelayTicks, queueDelay.Ticks);
                UpdateMaximum(ref _maximumQueueDelayTicks, queueDelay.Ticks);

                try
                {
                    await _reader(latestValue, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref _processedCount);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    Interlocked.Increment(ref _failureCount);
                    _logger?.LogError(exception, "Latest-value channel callback failed for {ValueType}", typeof(T).Name);
                }

                var processingDuration = _timeProvider.GetElapsedTime(processingStarted, _timeProvider.GetTimestamp());
                Interlocked.Exchange(ref _lastProcessingDurationTicks, processingDuration.Ticks);
                UpdateMaximum(ref _maximumProcessingDurationTicks, processingDuration.Ticks);
                PublishMetrics();

                if (_minimumInterval > TimeSpan.Zero)
                    await Task.Delay(_minimumInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
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
            _logger?.LogError(exception, "Latest-value channel metrics observer failed for {ValueType}", typeof(T).Name);
        }
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
