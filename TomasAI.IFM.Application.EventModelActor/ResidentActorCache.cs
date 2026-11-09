using System.Diagnostics.Metrics;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.EventModelActor;

/// <summary>Observed cache operations; residency is not a replacement for durable stream ownership.</summary>
public readonly record struct ResidentActorCacheStatistics(long Hits, long Misses, long Evictions,
    long Invalidations, long Fallbacks, int Entries, int PinnedEntries);

/// <summary>Hard-bounded per-actor cache with pinned owners and cancellation-aware same-key barriers.</summary>
internal sealed class ResidentActorCache<TValue>(string actor, ResidentActorPolicy policy, TimeProvider? clock = null)
    where TValue : class
{
    static readonly Meter Meter = new("TomasAI.IFM.ResidentActors");
    static readonly Counter<long> Operations = Meter.CreateCounter<long>("resident_actor.cache.operations");
    static readonly UpDownCounter<long> EntryCount = Meter.CreateUpDownCounter<long>("resident_actor.cache.entries");
    static readonly UpDownCounter<long> PinnedCount = Meter.CreateUpDownCounter<long>("resident_actor.cache.pinned");
    readonly object sync = new();
    readonly Dictionary<ActorThreadId, Entry> entries = [];
    readonly TimeProvider time = clock ?? TimeProvider.System;
    long hits, misses, evictions, invalidations, fallbacks;

    public ResidentActorCacheStatistics Statistics
    {
        get
        {
            lock (sync) return new(hits, misses, evictions, invalidations, fallbacks,
                entries.Count, entries.Values.Count(static entry => entry.Pinned));
        }
    }

    /// <summary>Waits only for the same key; a full cache of busy keys falls back without exceeding capacity.</summary>
    public async ValueTask<Lease?> AcquireAsync(ActorThreadId key, bool admit, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Task? barrier;
            lock (sync)
            {
                var now = time.GetUtcNow();
                if (entries.TryGetValue(key, out var entry))
                {
                    if (entry.Pinned) barrier = entry.Released.Task;
                    else
                    {
                        if (!admit || now - entry.LastUsed >= policy.IdleTimeout)
                        {
                            entries.Remove(key);
                            Gauge(EntryCount, -1);
                            evictions++;
                            Record("evict");
                        }
                        else
                        {
                            entry.Pin();
                            Gauge(PinnedCount, 1);
                            if (entry.Value is not null) { hits++; Record("hit"); }
                            else { misses++; Record("miss"); }
                            return new(this, key, entry);
                        }
                        barrier = null;
                    }
                }
                else barrier = null;

                if (barrier is null)
                {
                    if (!admit) return null;
                    TrimIdle(now);
                    if (entries.Count >= policy.MaximumResidentStreams)
                    {
                        var oldest = entries.Where(static pair => !pair.Value.Pinned)
                            .OrderBy(static pair => pair.Value.LastUsed).FirstOrDefault();
                        if (oldest.Value is null) { fallbacks++; Record("fallback"); return null; }
                        entries.Remove(oldest.Key);
                        Gauge(EntryCount, -1);
                        evictions++;
                        Record("evict");
                    }
                    var created = new Entry(now);
                    created.Pin();
                    entries.Add(key, created);
                    Gauge(EntryCount, 1);
                    Gauge(PinnedCount, 1);
                    misses++;
                    Record("miss");
                    return new(this, key, created);
                }
            }
            await barrier.WaitAsync(token).ConfigureAwait(false);
        }
    }

    /// <summary>Drains pinned work before discarding disposable state.</summary>
    public async ValueTask DrainAsync(CancellationToken token = default)
    {
        while (true)
        {
            Task[] pending;
            lock (sync)
            {
                pending = entries.Values.Where(static entry => entry.Pinned)
                    .Select(static entry => entry.Released.Task).ToArray();
                if (pending.Length == 0) { Gauge(EntryCount, -entries.Count); entries.Clear(); return; }
            }
            await Task.WhenAll(pending).WaitAsync(token).ConfigureAwait(false);
        }
    }

    void TrimIdle(DateTimeOffset now)
    {
        foreach (var key in entries.Where(pair => !pair.Value.Pinned && now - pair.Value.LastUsed >= policy.IdleTimeout)
                     .Select(static pair => pair.Key).ToArray())
        {
            entries.Remove(key);
            Gauge(EntryCount, -1);
            evictions++;
            Record("evict");
        }
    }

    void Record(string operation) => Operations.Add(1,
        new KeyValuePair<string, object?>("actor", actor), new KeyValuePair<string, object?>("operation", operation));

    void Gauge(UpDownCounter<long> counter, long change) => counter.Add(change, new KeyValuePair<string, object?>("actor", actor));

    void Release(ActorThreadId key, Entry entry)
    {
        lock (sync)
        {
            if ((entry.Invalid || entry.Value is null || !policy.Enabled) && entries.Remove(key))
                Gauge(EntryCount, -1);
            Gauge(PinnedCount, -1);
            entry.LastUsed = time.GetUtcNow();
            entry.Pinned = false;
            entry.Released.TrySetResult();
        }
    }

    internal sealed class Entry(DateTimeOffset now)
    {
        public TValue? Value;
        public bool Pinned;
        public bool Invalid;
        public DateTimeOffset LastUsed = now;
        public TaskCompletionSource Released = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Pin()
        {
            Pinned = true;
            Released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Pins a key until the request and any abandoned stage have both settled.</summary>
    internal sealed class Lease(ResidentActorCache<TValue> owner, ActorThreadId key, Entry entry) : IDisposable
    {
        readonly List<Task> abandoned = [];
        int disposed;

        public TValue? Value
        {
            get { lock (owner.sync) return entry.Invalid ? null : entry.Value; }
            set { lock (owner.sync) if (!entry.Invalid) entry.Value = value; }
        }

        public void Invalidate()
        {
            lock (owner.sync)
            {
                if (entry.Invalid) return;
                entry.Invalid = true;
                entry.Value = null;
                owner.invalidations++;
                owner.Record("invalidate");
            }
        }

        public void HoldUntil(Task task)
        {
            lock (owner.sync) abandoned.Add(task);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            Task[] pending;
            lock (owner.sync) pending = abandoned.ToArray();
            if (pending.All(static task => task.IsCompleted)) owner.Release(key, entry);
            else _ = ReleaseAfterAsync(pending);
        }

        async Task ReleaseAfterAsync(Task[] pending)
        {
            try { await Task.WhenAll(pending).ConfigureAwait(false); }
            catch { /* The Function lifecycle already observed and reported abandoned work. */ }
            finally { owner.Release(key, entry); }
        }
    }
}
