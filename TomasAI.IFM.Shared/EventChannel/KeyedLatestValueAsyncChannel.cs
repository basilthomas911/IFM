namespace TomasAI.IFM.Shared.EventChannel;

/// <summary>
/// Owns one bounded latest-value channel per key so independent state partitions cannot supersede each other.
/// </summary>
public sealed class KeyedLatestValueAsyncChannel<TKey, TValue> : IAsyncDisposable
    where TKey : notnull
{
    readonly object _gate = new();
    readonly Dictionary<TKey, LatestValueAsyncChannel<TValue>> _channels = [];
    readonly Func<TKey, TValue, CancellationToken, ValueTask> _reader;
    readonly Action<TKey, LatestValueChannelMetrics>? _metricsChanged;
    readonly TimeSpan _minimumInterval;
    readonly TimeProvider _timeProvider;
    Task? _stopTask;
    bool _acceptingWrites = true;

    /// <summary>
    /// Creates an open keyed latest-value channel collection.
    /// </summary>
    public KeyedLatestValueAsyncChannel(
        Func<TKey, TValue, CancellationToken, ValueTask> reader,
        TimeSpan minimumInterval = default,
        TimeProvider? timeProvider = null,
        Action<TKey, LatestValueChannelMetrics>? metricsChanged = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (minimumInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(minimumInterval));
        _reader = reader;
        _minimumInterval = minimumInterval;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _metricsChanged = metricsChanged;
    }

    /// <summary>Gets whether new keyed values are accepted.</summary>
    public bool IsOpen
    {
        get
        {
            lock (_gate)
                return _acceptingWrites;
        }
    }

    /// <summary>Gets a metrics snapshot for every key observed during the current run.</summary>
    public IReadOnlyDictionary<TKey, LatestValueChannelMetrics> Metrics
    {
        get
        {
            lock (_gate)
                return _channels.ToDictionary(pair => pair.Key, pair => pair.Value.Metrics);
        }
    }

    /// <summary>
    /// Attempts to write a value to its key partition. A new capacity-one partition is created on first use.
    /// </summary>
    public bool TryWrite(TKey key, TValue value)
    {
        LatestValueAsyncChannel<TValue> channel;
        lock (_gate)
        {
            if (!_acceptingWrites)
                return false;
            if (!_channels.TryGetValue(key, out channel!))
            {
                channel = new LatestValueAsyncChannel<TValue>(
                    (item, cancellationToken) => _reader(key, item, cancellationToken),
                    _minimumInterval,
                    timeProvider: _timeProvider,
                    metricsChanged: metrics => _metricsChanged?.Invoke(key, metrics));
                _channels.Add(key, channel);
                _metricsChanged?.Invoke(key, channel.Metrics);
            }
        }
        return channel.TryWrite(value);
    }

    /// <summary>Stops all key partitions and waits for their active readers to finish.</summary>
    public ValueTask StopAsync()
    {
        Task stopTask;
        lock (_gate)
        {
            if (_stopTask is not null)
                return new ValueTask(_stopTask);
            _acceptingWrites = false;
            var channels = _channels.Values.ToArray();
            _stopTask = stopTask = StopChannelsAsync(channels);
        }
        return new ValueTask(stopTask);

        static async Task StopChannelsAsync(LatestValueAsyncChannel<TValue>[] channels)
            => await Task.WhenAll(channels.Select(channel => channel.StopAsync().AsTask())).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => StopAsync();
}
