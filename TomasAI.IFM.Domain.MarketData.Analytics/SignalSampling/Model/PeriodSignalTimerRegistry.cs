using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.MarketData.Analytics.SignalSampling.Model;

internal static class PeriodSignalTimerRegistry<TEntityId> where TEntityId : notnull
{
    static readonly ConcurrentDictionary<TEntityId, Registration> Timers = new();

    public static bool Start(TEntityId entityId, Func<TEntityId, ValueTask> callback, TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(period, TimeSpan.Zero);

        var registration = new Registration(entityId, callback, period);
        if (!Timers.TryAdd(entityId, registration))
        {
            registration.DisposeUnstarted();
            return false;
        }
        registration.StartLoop();
        return true;
    }

    public static async ValueTask<bool> StopAsync(TEntityId entityId)
    {
        if (!Timers.TryGetValue(entityId, out var registration))
            return false;
        try
        {
            await registration.StopRegistrationAsync().ConfigureAwait(false);
        }
        finally
        {
            Timers.TryRemove(new KeyValuePair<TEntityId, Registration>(entityId, registration));
        }
        return true;
    }

    public static bool TryAcceptSourceSequence(TEntityId entityId, long sourceSequence)
        => sourceSequence >= 0
           && Timers.TryGetValue(entityId, out var registration)
           && registration.TryAcceptSequence(sourceSequence);

    public static async ValueTask StopAllAsync()
    {
        var registrations = Timers.ToArray();
        try
        {
            await Task.WhenAll(registrations.Select(static item => item.Value.StopRegistrationAsync().AsTask())).ConfigureAwait(false);
        }
        finally
        {
            foreach (var registration in registrations)
                Timers.TryRemove(registration);
        }
    }

    sealed class Registration(TEntityId entityId, Func<TEntityId, ValueTask> callback, TimeSpan period) : IAsyncDisposable
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

        public void StartLoop()
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

        public ValueTask DisposeAsync() => StopRegistrationAsync();

        public ValueTask StopRegistrationAsync()
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
