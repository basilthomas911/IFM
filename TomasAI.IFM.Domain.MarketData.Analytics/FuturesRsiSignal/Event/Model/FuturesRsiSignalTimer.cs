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
        => StartTimer(e, timerAction, GetTimerPeriod(e.EntityId));

    internal static bool StartTimer(
        this FuturesRsiSignalStartedEvent e,
        Func<FuturesRsiSignalEntityId, ValueTask> timerAction,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(timerAction);
        if (period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(period));

        var registration = new TimerRegistration(e.EntityId, timerAction, period);
        if (!Timers.TryAdd(e.EntityId, registration))
            return false;

        registration.Start();
        return true;
    }

    /// <summary>
    /// Stops the callback loop and waits for an in-flight callback to finish.
    /// </summary>
    public static async ValueTask<bool> StopTimerAsync(this FuturesRsiSignalStoppedEvent e)
    {
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
            TimeFrameType.Daily => TimeSpan.FromMinutes(1),
            TimeFrameType.Weekly => TimeSpan.FromMinutes(15),
            TimeFrameType.WeekMonthBridge => TimeSpan.FromHours(1),
            TimeFrameType.Monthly => TimeSpan.FromDays(1),
            _ => TimeSpan.FromMinutes(1)
        };

    sealed class TimerRegistration(
        FuturesRsiSignalEntityId entityId,
        Func<FuturesRsiSignalEntityId, ValueTask> callback,
        TimeSpan period)
    {
        readonly CancellationTokenSource _stopping = new();
        readonly object _lifecycleLock = new();
        Task _loopTask = Task.CompletedTask;
        bool _started;
        bool _stopped;

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

        public async ValueTask StopAsync()
        {
            Task loopTask;
            lock (_lifecycleLock)
            {
                if (!_stopped)
                {
                    _stopped = true;
                    _stopping.Cancel();
                }
                loopTask = _loopTask;
            }

            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
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
