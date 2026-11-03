namespace TomasAI.IFM.Application.MarketData.UnitTests.Harness;

/// <summary>Advances fixture timestamps and deadline timers together, independently of machine load.</summary>
internal class ManualTestTimeProvider(DateTimeOffset initial) : TimeProvider
{
    private readonly object gate = new();
    private readonly List<ManualTimer> timers = [];
    private DateTimeOffset now = initial;

    public override DateTimeOffset GetUtcNow() { lock (gate) return now; }
    public override long GetTimestamp() => GetUtcNow().UtcTicks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        ManualTimer[] pending;
        lock (gate) { now += duration; pending = [.. timers]; }
        foreach (var timer in pending) timer.FireIfDue();
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        lock (gate) { timers.Add(timer); timer.Change(dueTime, period); }
        return timer;
    }

    private sealed class ManualTimer(ManualTestTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        private DateTimeOffset? due;
        private TimeSpan period;
        private bool disposed;

        public bool Change(TimeSpan dueTime, TimeSpan nextPeriod)
        {
            lock (clock.gate)
            {
                if (disposed) return false;
                due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.now + dueTime;
                period = nextPeriod;
                return true;
            }
        }

        internal void FireIfDue()
        {
            lock (clock.gate)
            {
                if (disposed || due is null || due > clock.now) return;
                due = period > TimeSpan.Zero ? clock.now + period : null;
            }
            callback(state);
        }

        public void Dispose() { lock (clock.gate) { disposed = true; clock.timers.Remove(this); } }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
