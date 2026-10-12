using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;

/// <summary>
/// Owns one non-overlapping asynchronous bar callback per streaming entity.
/// </summary>
public sealed class FuturesBarDataTimer : IFuturesBarDataTimer
{
    public static readonly TimeSpan DefaultPeriod = TimeSpan.FromSeconds(15);
    readonly ConcurrentDictionary<FuturesBarDataStreamingId, Registration> _registrations = new();
    readonly TimeSpan _period;

    public bool IsRunning(FuturesBarDataStreamingId entityId)
        => _registrations.TryGetValue(entityId, out var registration) && registration.IsRunning;

    public FuturesBarDataTimer() : this(DefaultPeriod)
    {
    }

    public FuturesBarDataTimer(TimeSpan period)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);
        _period = period;
    }

    public bool Start(FuturesBarDataStreamingId entityId, Func<ValueTask> timerAction)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(timerAction);

        if (_registrations.TryGetValue(entityId, out var previous) && previous.IsTerminal)
            _registrations.TryRemove(new KeyValuePair<FuturesBarDataStreamingId, Registration>(entityId, previous));
        var registration = new Registration(timerAction, _period);
        if (!_registrations.TryAdd(entityId, registration))
        {
            registration.DisposeUnstarted();
            return false;
        }

        registration.Start();
        return true;
    }

    public async ValueTask<bool> StopAsync(FuturesBarDataStreamingId entityId)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        if (!_registrations.TryGetValue(entityId, out var registration))
            return false;

        try
        {
            await registration.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            _registrations.TryRemove(
                new KeyValuePair<FuturesBarDataStreamingId, Registration>(entityId, registration));
        }

        return true;
    }

    public async ValueTask StopAllAsync()
    {
        var registrations = _registrations.ToArray();
        try
        {
            await Task.WhenAll(
                registrations.Select(static item => item.Value.StopAsync().AsTask())).ConfigureAwait(false);
        }
        finally
        {
            foreach (var registration in registrations)
                _registrations.TryRemove(registration);
        }
    }

    sealed class Registration(Func<ValueTask> callback, TimeSpan period) : IAsyncDisposable
    {
        readonly CancellationTokenSource _stopping = new();
        readonly System.Threading.Lock _lifecycleLock = new();
        Task _loopTask = Task.CompletedTask;
        Task? _stopTask;
        Task _cancellationTask = Task.CompletedTask;
        bool _started;
        bool _stopped;
        internal bool IsTerminal { get { lock (_lifecycleLock) return _started && _loopTask.IsCompleted; } }
        internal bool IsRunning { get { lock (_lifecycleLock) return _started && !_stopped && !_loopTask.IsCompleted; } }

        public void Start()
        {
            lock (_lifecycleLock)
            {
                if (_started || _stopped)
                    return;

                _started = true;
                _loopTask = RunAsync();
            }
        }

        /// <summary>Releases a rejected registration before it owns a callback loop.</summary>
        public void DisposeUnstarted()
        {
            lock (_lifecycleLock)
            {
                if (_started || _stopped) return;
                _stopped = true;
                _stopTask = Task.CompletedTask;
                _stopping.Dispose();
            }
        }

        public ValueTask DisposeAsync() => StopAsync();

        public ValueTask StopAsync()
        {
            lock (_lifecycleLock)
            {
                if (!_stopped)
                {
                    _stopped = true;
                    _cancellationTask = _stopping.CancelAsync();
                }
                return new ValueTask(_stopTask ??= CompleteStopAsync());
            }
        }

        async Task CompleteStopAsync()
        {
            try
            {
                await Task.WhenAll(_cancellationTask, _loopTask).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                // Expected completion after the registration is stopped.
            }
            finally
            {
                _stopping.Dispose();
            }
        }

        async Task RunAsync()
        {
            using var timer = new PeriodicTimer(period);
            while (await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false))
                await callback().ConfigureAwait(false);
        }
    }
}
