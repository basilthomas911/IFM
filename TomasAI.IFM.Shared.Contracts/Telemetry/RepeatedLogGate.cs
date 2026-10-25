namespace TomasAI.IFM.Shared.Telemetry;

/// <summary>Fixed-memory repetition gate. Hash collisions permit a log rather than suppress another subject's first occurrence.</summary>
public sealed class RepeatedLogGate<TKey> where TKey : notnull
{
    sealed class Entry { internal TKey Key = default!; internal bool HasKey; internal long Started; internal long Suppressed; }
    readonly Entry[] entries;
    readonly TimeProvider clock;
    readonly TimeSpan interval;
    public RepeatedLogGate(TimeSpan interval, int capacity = 128, TimeProvider? clock = null)
    {
        if (interval <= TimeSpan.Zero || capacity <= 0) throw new ArgumentOutOfRangeException(nameof(interval));
        this.interval = interval; this.clock = clock ?? TimeProvider.System;
        entries = Enumerable.Range(0, capacity).Select(_ => new Entry()).ToArray();
    }
    public bool ShouldLog(TKey key, out long suppressedCount)
    {
        var entry = entries[(uint)EqualityComparer<TKey>.Default.GetHashCode(key) % (uint)entries.Length];
        lock (entry)
        {
            var now = clock.GetTimestamp();
            if (!entry.HasKey || !EqualityComparer<TKey>.Default.Equals(entry.Key, key))
            {
                entry.Key = key; entry.HasKey = true; entry.Started = now; entry.Suppressed = 0;
                suppressedCount = 0; return true;
            }
            if (clock.GetElapsedTime(entry.Started, now) >= interval)
            {
                suppressedCount = entry.Suppressed; entry.Suppressed = 0; entry.Started = now; return true;
            }
            entry.Suppressed++; suppressedCount = 0; return false;
        }
    }
}
