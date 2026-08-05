using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Event;

internal static class PeriodSignalTimerPeriod
{
    public static TimeSpan Get(TimeFrameType timePeriod) => timePeriod switch
    {
        TimeFrameType.Daily => TimeSpan.FromMinutes(1),
        TimeFrameType.Weekly => TimeSpan.FromMinutes(15),
        TimeFrameType.WeekMonthBridge => TimeSpan.FromHours(1),
        TimeFrameType.Monthly => TimeSpan.FromDays(1),
        _ => TimeSpan.FromMinutes(1)
    };
}

internal static class PeriodSignalTimerRegistry<TEntityId> where TEntityId : notnull
{
    static readonly ConcurrentDictionary<TEntityId, Registration> Timers = new();

    public static bool Start(TEntityId entityId, Func<TEntityId, ValueTask> callback, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(period));

        var registration = new Registration(entityId, callback, period);
        if (!Timers.TryAdd(entityId, registration))
            return false;
        registration.Start();
        return true;
    }

    public static async ValueTask<bool> StopAsync(TEntityId entityId)
    {
        if (!Timers.TryGetValue(entityId, out var registration))
            return false;
        try
        {
            await registration.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            Timers.TryRemove(new KeyValuePair<TEntityId, Registration>(entityId, registration));
        }
        return true;
    }

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

    sealed class Registration(TEntityId entityId, Func<TEntityId, ValueTask> callback, TimeSpan period)
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
