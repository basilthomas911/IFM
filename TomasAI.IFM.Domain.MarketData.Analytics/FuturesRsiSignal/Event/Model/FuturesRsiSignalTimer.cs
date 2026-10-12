using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Event.Model;

/// <summary>
/// Owns the recurring RSI work scheduled by start/stop domain events.
/// </summary>
public static class FuturesRsiSignalTimer
{
    static readonly ConcurrentDictionary<FuturesRsiSignalEntityId, TimerRegistration> Timers = new();

    /// <summary>
    /// Starts one non-overlapping asynchronous callback loop for an RSI signal entity.
    /// A duplicate start is idempotent and leaves the existing loop in place.
    /// </summary>
    public static bool StartTimer(
        this FuturesRsiSignalStartedEvent e,
        Func<FuturesRsiSignalEntityId, ValueTask> timerAction)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        return StartTimer(e, timerAction, GetTimerPeriod(e.EntityId));
    }

    internal static bool StartTimer(
        this FuturesRsiSignalStartedEvent e,
        Func<FuturesRsiSignalEntityId, ValueTask> timerAction,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(timerAction);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);

        var registration = new TimerRegistration(e.EntityId, timerAction, period);
        if (!Timers.TryAdd(e.EntityId, registration))
        {
            registration.DisposeUnstarted();
            return false;
        }

        registration.Start();
        return true;
    }

    /// <summary>
    /// Accepts a source sequence once for the active timer registration.
    /// </summary>
    public static bool TryAcceptSourceSequence(this FuturesRsiSignalStartedEvent e, long sourceSequence)
        
    {
        ArgumentNullException.ThrowIfNull(e);
        return sourceSequence >= 0
           && Timers.TryGetValue(e.EntityId, out var registration)
           && registration.TryAcceptSequence(sourceSequence);
    }

    /// <summary>
    /// Stops the callback loop and waits for an in-flight callback to finish.
    /// </summary>
    public static async ValueTask<bool> StopTimerAsync(this FuturesRsiSignalStoppedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!Timers.TryGetValue(e.EntityId, out var registration))
            return false;

        try
        {
            await registration.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            Timers.TryRemove(new KeyValuePair<FuturesRsiSignalEntityId, TimerRegistration>(e.EntityId, registration));
        }
        return true;
    }

    /// <summary>
    /// Stops every loop owned by this actor type during actor shutdown.
    /// </summary>
    public static async ValueTask StopAllAsync()
    {
        var registrations = Timers.ToArray();
        try
        {
            await Task.WhenAll(registrations.Select(static item => item.Value.StopAsync().AsTask())).ConfigureAwait(false);
        }
        finally
        {
            foreach (var registration in registrations)
                Timers.TryRemove(registration);
        }
    }

    static TimeSpan GetTimerPeriod(FuturesRsiSignalEntityId rsiSignalId)
        => rsiSignalId.TimePeriod switch
        {
            TimeFrameType.TenSeconds => TimeSpan.FromSeconds(10),
            TimeFrameType.FifteenSeconds => TimeSpan.FromSeconds(15),
            TimeFrameType.OneMinute => TimeSpan.FromMinutes(1),
            TimeFrameType.FiveMinutes => TimeSpan.FromMinutes(5),
            TimeFrameType.TenMinutes => TimeSpan.FromMinutes(10),
            TimeFrameType.FifteenMinutes => TimeSpan.FromMinutes(15),
            TimeFrameType.ThirtyMinutes => TimeSpan.FromMinutes(30),
            TimeFrameType.OneHour => TimeSpan.FromHours(1),
            TimeFrameType.FourHours => TimeSpan.FromHours(4),
            TimeFrameType.Daily => TimeSpan.FromMinutes(1),
            TimeFrameType.Weekly => TimeSpan.FromMinutes(15),
            TimeFrameType.WeekMonthBridge => TimeSpan.FromHours(1),
            TimeFrameType.Monthly => TimeSpan.FromDays(1),
            _ => throw new ArgumentOutOfRangeException(
                nameof(rsiSignalId),
                rsiSignalId.TimePeriod,
                "Unsupported RSI sampling period")
        };

    sealed class TimerRegistration(
        FuturesRsiSignalEntityId entityId,
        Func<FuturesRsiSignalEntityId, ValueTask> callback,
        TimeSpan period) : IAsyncDisposable
    {
        readonly CancellationTokenSource _stopping = new();
        readonly System.Threading.Lock _lifecycleLock = new();
        Task _loopTask = Task.CompletedTask;
        Task? _stopTask;
        Task _cancellationTask = Task.CompletedTask;
        bool _started;
        bool _stopped;
        long _lastSourceSequence;

        public bool TryAcceptSequence(long sourceSequence)
        {
            while (true)
            {
                var previous = Volatile.Read(ref _lastSourceSequence);
                if (sourceSequence <= previous)
                    return false;
                if (Interlocked.CompareExchange(ref _lastSourceSequence, sourceSequence, previous) == previous)
                    return true;
            }
        }

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
            await Task.Yield();
            using var timer = new PeriodicTimer(period);

            while (!_stopping.IsCancellationRequested)
            {
                await callback(entityId).ConfigureAwait(false);
                if (!await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false))
                    break;
            }
        }
    }
}
