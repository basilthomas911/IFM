namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.TestDoubles;

/// <summary>
/// Provides deterministic wall-clock, timestamp, delay, and timer behavior for
/// lifecycle and periodic-operation tests.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    readonly object _gate = new();
    readonly List<ManualTimer> _timers = [];
    DateTimeOffset _utcNow;
    long _timestamp;

    /// <summary>
    /// Creates a provider at a known UTC instant.
    /// </summary>
    public ManualTimeProvider(DateTimeOffset utcNow)
        => _utcNow = utcNow.ToUniversalTime();

    /// <inheritdoc />
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
            return _utcNow;
    }

    /// <inheritdoc />
    public override long GetTimestamp()
    {
        lock (_gate)
            return _timestamp;
    }

    /// <inheritdoc />
    public override ITimer CreateTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    /// <summary>
    /// Advances virtual time and synchronously fires all timers due on or before
    /// the resulting instant.
    /// </summary>
    public void Advance(TimeSpan amount)
    {
        if (amount < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Time cannot move backwards.");

        DateTimeOffset target;
        lock (_gate)
            target = _utcNow + amount;

        while (true)
        {
            List<(TimerCallback Callback, object? State)> callbacks;
            lock (_gate)
            {
                var nextDue = _timers
                    .Where(timer => timer.DueAt is not null)
                    .Select(timer => timer.DueAt!.Value)
                    .Where(dueAt => dueAt <= target)
                    .DefaultIfEmpty(DateTimeOffset.MaxValue)
                    .Min();

                if (nextDue == DateTimeOffset.MaxValue)
                {
                    MoveClockTo(target);
                    return;
                }

                MoveClockTo(nextDue);
                callbacks = _timers
                    .Where(timer => timer.DueAt is not null && timer.DueAt <= _utcNow)
                    .Select(timer => timer.TakeCallback())
                    .Where(callback => callback is not null)
                    .Select(callback => callback!.Value)
                    .ToList();
            }

            foreach (var callback in callbacks)
                callback.Callback(callback.State);
        }
    }

    void MoveClockTo(DateTimeOffset value)
    {
        if (value <= _utcNow)
            return;

        _timestamp = checked(_timestamp + (value - _utcNow).Ticks);
        _utcNow = value;
    }

    void Register(ManualTimer timer)
    {
        lock (_gate)
            _timers.Add(timer);
    }

    bool Change(ManualTimer timer, TimeSpan dueTime, TimeSpan period)
    {
        ValidateTimeout(dueTime, nameof(dueTime));
        ValidateTimeout(period, nameof(period));
        lock (_gate)
            return timer.ChangeCore(_utcNow, dueTime, period);
    }

    void Dispose(ManualTimer timer)
    {
        lock (_gate)
        {
            timer.DisposeCore();
            _timers.Remove(timer);
        }
    }

    static void ValidateTimeout(TimeSpan value, string parameterName)
    {
        if (value < TimeSpan.Zero && value != Timeout.InfiniteTimeSpan)
            throw new ArgumentOutOfRangeException(parameterName, value, "Timeout must be non-negative or infinite.");
    }

    sealed class ManualTimer : ITimer
    {
        readonly ManualTimeProvider _owner;
        readonly TimerCallback _callback;
        readonly object? _state;
        TimeSpan _period;
        bool _disposed;

        public ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state)
        {
            _owner = owner;
            _callback = callback;
            _state = state;
            owner.Register(this);
        }

        public DateTimeOffset? DueAt { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
            => _owner.Change(this, dueTime, period);

        public void Dispose() => _owner.Dispose(this);

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }

        public bool ChangeCore(DateTimeOffset now, TimeSpan dueTime, TimeSpan period)
        {
            if (_disposed)
                return false;

            _period = period;
            DueAt = dueTime == Timeout.InfiniteTimeSpan ? null : now + dueTime;
            return true;
        }

        public (TimerCallback Callback, object? State)? TakeCallback()
        {
            if (_disposed || DueAt is null)
                return null;

            DueAt = _period > TimeSpan.Zero
                ? DueAt.Value + _period
                : null;
            return (_callback, _state);
        }

        public void DisposeCore()
        {
            _disposed = true;
            DueAt = null;
        }
    }
}
