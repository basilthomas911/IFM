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

    public FuturesBarDataTimer() : this(DefaultPeriod)
    {
    }

    public FuturesBarDataTimer(TimeSpan period)
    {
        if (period <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(period));
        _period = period;
    }

    public bool Start(FuturesBarDataStreamingId entityId, Func<ValueTask> timerAction)
    {
        ArgumentNullException.ThrowIfNull(entityId);
        ArgumentNullException.ThrowIfNull(timerAction);

        var registration = new Registration(timerAction, _period);
        if (!_registrations.TryAdd(entityId, registration))
            return false;

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

    sealed class Registration(Func<ValueTask> callback, TimeSpan period)
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
            using var timer = new PeriodicTimer(period);
            while (await timer.WaitForNextTickAsync(_stopping.Token).ConfigureAwait(false))
                await callback().ConfigureAwait(false);
        }
    }
}

public interface IFuturesBarDataTimer
{
    bool Start(FuturesBarDataStreamingId entityId, Func<ValueTask> timerAction);
    ValueTask<bool> StopAsync(FuturesBarDataStreamingId entityId);
    ValueTask StopAllAsync();
}
