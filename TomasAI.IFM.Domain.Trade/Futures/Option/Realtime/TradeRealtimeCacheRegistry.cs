using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

/// <summary>Host-owned bounded cache, shared by pricing ingress, position monitoring and read-only queries.</summary>
public sealed class TradeRealtimeCacheRegistry(TimeProvider? time = null)
{
    readonly object gate = new();
    readonly ConcurrentDictionary<TradeEntityId, TradeRealtimeHotCache> trades = new();
    readonly ConcurrentDictionary<string, TradeRealtimeHotCache[]> publishedRoutes = new(StringComparer.Ordinal);
    readonly TimeProvider clock = time ?? TimeProvider.System;
    readonly object expiryGate = new();
    readonly ConcurrentDictionary<TradeRealtimeHotCache, byte> changedDeadlines = new();
    readonly Dictionary<TradeRealtimeHotCache, long> deadlines = [];
    readonly PriorityQueue<(TradeRealtimeHotCache Cache, long Deadline), long> expiry = new();
    int residentCount;
    int dirtyDeadline;
    long nextWakeTicks = DateTimeOffset.MaxValue.UtcTicks;
    bool rebuildExpiry;
    readonly Dictionary<string, HashSet<TradeEntityId>> routes = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<TradeEntityId, (long RouteGeneration, long PositionSequence)> publications = new();
    /// <summary>Captures current trade evidence without changing the publication throttle.</summary>
    public TradeRealtimeSnapshot[] CaptureRecoveryObservations() => trades.Values.Select(cache => cache.Read()).ToArray();
    /// <summary>Records only completed downstream sends, never attempted publication.</summary>
    public void RecordPublication(TradeRealtimeSnapshot snapshot)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        if (snapshot.Position is not { } position) return;
        publications.AddOrUpdate(snapshot.TradeId, (position.RouteGeneration, position.PositionSequence), (_, previous) =>
            position.RouteGeneration > previous.RouteGeneration ? (position.RouteGeneration, position.PositionSequence)
                : position.RouteGeneration == previous.RouteGeneration ? (previous.RouteGeneration, Math.Max(previous.PositionSequence, position.PositionSequence)) : previous);
    }
    public bool HasPublishedPosition(TradeEntityId trade, long generation, long sequence) => publications.TryGetValue(trade, out var published)
        && published.RouteGeneration == generation && published.PositionSequence >= sequence;
    public TradeRealtimeHotCache Register(EstablishedTradeDefinition trade, DateOnly date)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        lock (gate)
        {
            if (trades.TryGetValue(trade.Id, out var current))
            {
                var snapshot = current.Read();
                if (snapshot.ValueDate > date || snapshot.ValueDate == date && (snapshot.CompositionVersion > trade.EvidenceRevision
                    || snapshot.CompositionVersion == trade.EvidenceRevision
                        && snapshot.Legs.Select(leg => leg.Definition).SequenceEqual(trade.Legs))) return current;
            }
            Remove(trade.Id);
            if (residentCount >= 4096) throw new InvalidOperationException("Trade realtime cache capacity exceeded.");
            var cache = new TradeRealtimeHotCache(trade, date, clock, OnDeadlineChanged);
            trades[trade.Id] = cache;
            Interlocked.Increment(ref residentCount);
            foreach (var leg in trade.Legs)
            {
                if (!routes.TryGetValue(leg.ContractId, out var ids)) routes[leg.ContractId] = ids = [];
                ids.Add(trade.Id);
                publishedRoutes[leg.ContractId] = ids.Select(id => trades[id]).ToArray();
            }
            return cache;
        }
    }
    public TradeRealtimeHotCache? Find(TradeEntityId id)
    {
        return trades.GetValueOrDefault(id);
    }
    public TradeRealtimeSnapshot[] Expire()
        => Expire(null);

    /// <summary>Retains busy trades' deadlines while independent trades continue expiring; the caller owns the stable blocked set during this synchronous call.</summary>
    public TradeRealtimeSnapshot[] Expire(IReadOnlySet<TradeEntityId>? blocked)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        var now = clock.GetUtcNow().UtcTicks;
        if (Volatile.Read(ref dirtyDeadline) == 0 && now < Interlocked.Read(ref nextWakeTicks)) return [];
        lock (expiryGate)
        {
            if (Interlocked.Exchange(ref dirtyDeadline, 0) != 0)
                foreach (var cache in changedDeadlines.Keys)
                    if (changedDeadlines.TryRemove(cache, out _))
                    {
                        // Acknowledge before reading the deadline. A concurrent update
                        // is then either included in this read or queues the next one.
                        cache.AcknowledgeDeadlineChange();
                        if (IsCurrent(cache)) Schedule(cache);
                    }
            if (expiry.Count >= 64 && expiry.TryPeek(out _, out var firstDue) && firstDue <= now && IsDenseExpiry(now))
            {
                // Heap maintenance is useful for sparse deadlines. A dense burst
                // is cheaper as one bounded pass, retaining the same cache locks
                // and generation fences rather than dequeuing every heap entry.
                var scheduled = deadlines.Keys.ToArray();
                expiry.Clear(); deadlines.Clear();
                List<TradeRealtimeSnapshot>? burst = null;
                foreach (var cache in scheduled)
                {
                    var changed = blocked?.Contains(cache.Read().TradeId) != true && cache.NextQualityCheckTicks <= now
                        && cache.ExpireFromScheduler();
                    if (!IsCurrent(cache)) continue;
                    if (changed) (burst ??= []).Add(cache.ReadForPublication());
                    Schedule(cache);
                }
                rebuildExpiry = false;
                Interlocked.Exchange(ref nextWakeTicks, expiry.TryPeek(out _, out var nextBurst) ? nextBurst : DateTimeOffset.MaxValue.UtcTicks);
                return burst?.ToArray() ?? [];
            }
            List<TradeRealtimeHotCache>? due = null;
            List<((TradeRealtimeHotCache Cache, long Deadline) Entry, long Priority)>? deferred = null;
            while (expiry.TryPeek(out var entry, out var deadline) && deadline <= now)
            {
                expiry.Dequeue();
                if (!deadlines.TryGetValue(entry.Cache, out var current) || current != entry.Deadline) continue;
                if (blocked?.Contains(entry.Cache.Read().TradeId) == true)
                {
                    (deferred ??= []).Add((entry, deadline));
                    continue;
                }
                // Removing the current schedule here deduplicates obsolete entries
                // without a second hash set. A concurrent quote schedules its new
                // deadline through the dirty queue or the post-expiry reschedule.
                deadlines.Remove(entry.Cache);
                (due ??= []).Add(entry.Cache);
            }
            if (deferred is not null)
                foreach (var item in deferred) expiry.Enqueue(item.Entry, item.Priority);
            List<TradeRealtimeSnapshot>? expired = null;
            if (due is not null)
                foreach (var cache in due)
                {
                    var changed = cache.ExpireFromScheduler();
                    if (IsCurrent(cache))
                    {
                        if (changed) (expired ??= []).Add(cache.ReadForPublication());
                        Schedule(cache);
                    }
                }
            // Lazy obsolete entries are bounded; never retain one heap entry per incoming quote.
            if (rebuildExpiry || expiry.Count > Math.Max(128, Volatile.Read(ref residentCount) * 2))
            {
                expiry.Clear();
                foreach (var pair in deadlines) expiry.Enqueue((pair.Key, pair.Value), pair.Value);
                rebuildExpiry = false;
            }
            Interlocked.Exchange(ref nextWakeTicks, expiry.TryPeek(out _, out var next) ? next : DateTimeOffset.MaxValue.UtcTicks);
            return expired?.ToArray() ?? [];
        }
    }

    void OnDeadlineChanged(TradeRealtimeHotCache cache)
    {
        if (!cache.TryMarkDeadlineChange()) return;
        changedDeadlines.TryAdd(cache, 0);
        Volatile.Write(ref dirtyDeadline, 1);
    }
    bool IsDenseExpiry(long now)
    {
        var remaining = Math.Max(32, deadlines.Count / 2);
        foreach (var entry in expiry.UnorderedItems)
            if (entry.Priority <= now && --remaining == 0) return true;
        return false;
    }
    bool IsCurrent(TradeRealtimeHotCache cache) => trades.TryGetValue(cache.Read().TradeId, out var current) && ReferenceEquals(cache, current);
    void Schedule(TradeRealtimeHotCache cache)
    {
        var next = cache.NextQualityCheckTicks;
        if (next == DateTimeOffset.MaxValue.UtcTicks) { deadlines.Remove(cache); return; }
        if (deadlines.TryGetValue(cache, out var previous) && previous == next) return;
        deadlines[cache] = next;
        expiry.Enqueue((cache, next), next);
    }
    public TradeRealtimeSnapshot[] Apply(FuturesOptionTickDataV2ReadModel observation)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        if (!publishedRoutes.TryGetValue(observation.ContractId, out var interested)) return [];
        TradeRealtimeSnapshot[]? changed = null;
        var count = 0;
        foreach (var cache in interested)
            if (cache.Apply(observation))
            {
                changed ??= new TradeRealtimeSnapshot[interested.Length];
                changed[count++] = cache.ReadForPublication();
            }
        if (changed is null) return [];
        if (count != changed.Length) Array.Resize(ref changed, count);
        return changed;
    }
    public void Remove(TradeEntityId id)
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        publications.TryRemove(id, out _);
        lock (gate)
        {
            if (!trades.TryRemove(id, out var cache)) return;
            Interlocked.Decrement(ref residentCount);
            cache.Retire();
            changedDeadlines.TryRemove(cache, out _);
            lock (expiryGate)
            {
                deadlines.Remove(cache);
                rebuildExpiry = true; // Release retired cache references on the next poll, regardless of their old deadline.
                Volatile.Write(ref dirtyDeadline, 1);
            }
            foreach (var leg in cache.Read().Legs)
                if (routes.TryGetValue(leg.Definition.ContractId, out var ids))
                {
                    ids.Remove(id);
                    if (ids.Count == 0)
                    {
                        routes.Remove(leg.Definition.ContractId);
                        publishedRoutes.TryRemove(leg.Definition.ContractId, out _);
                    }
                    else publishedRoutes[leg.Definition.ContractId] = ids.Select(key => trades[key]).ToArray();
                }
        }
    }
}
