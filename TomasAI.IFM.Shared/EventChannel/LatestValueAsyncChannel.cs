using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Shared.EventChannel;

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
    readonly TimeSpan _minimumInterval;
    readonly TimeProvider _timeProvider;
    readonly ILogger? _logger;
    readonly CancellationTokenSource _stopSource = new();
    readonly Task _processingTask;
    int _acceptingWrites = 1;
    int _stopSourceDisposed;

    /// <summary>
    /// Creates and starts a latest-value channel.
    /// </summary>
    /// <param name="reader">The serialized asynchronous callback that processes the latest value.</param>
    /// <param name="minimumInterval">The minimum delay after one callback completes before another begins.</param>
    /// <param name="logger">An optional logger for callback failures.</param>
    /// <param name="timeProvider">An optional time provider used for the minimum interval.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minimumInterval"/> is negative.</exception>
    public LatestValueAsyncChannel(
        Func<T, CancellationToken, ValueTask> reader,
        TimeSpan minimumInterval = default,
        ILogger? logger = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (minimumInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumInterval), minimumInterval, "The minimum interval cannot be negative.");

        _reader = reader;
        _minimumInterval = minimumInterval;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
    /// Attempts to publish a value. A successful write can replace an older pending value.
    /// </summary>
    /// <param name="value">The newest replaceable value.</param>
    /// <returns><see langword="true"/> when accepted; otherwise <see langword="false"/> after shutdown.</returns>
    public bool TryWrite(T value)
        => IsOpen && _channel.Writer.TryWrite(value);

    /// <summary>
    /// Stops accepting writes, cancels current processing, and asynchronously waits for the reader loop to finish.
    /// Pending display state is discarded because it is no longer useful after its owner closes.
    /// </summary>
    public async ValueTask StopAsync()
    {
        if (Interlocked.Exchange(ref _acceptingWrites, 0) == 1)
        {
            _channel.Writer.TryComplete();
            _stopSource.Cancel();
        }

        try
        {
            await _processingTask.ConfigureAwait(false);
        }
        finally
        {
            if (Interlocked.Exchange(ref _stopSourceDisposed, 1) == 0)
                _stopSource.Dispose();
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
                if (!_channel.Reader.TryRead(out var latestValue))
                    continue;

                while (_channel.Reader.TryRead(out var newerValue))
                    latestValue = newerValue;

                try
                {
                    await _reader(latestValue, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger?.LogError(exception, "Latest-value channel callback failed for {ValueType}", typeof(T).Name);
                }

                if (_minimumInterval > TimeSpan.Zero)
                    await Task.Delay(_minimumInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
