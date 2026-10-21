using System.Collections.Concurrent;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

/// <summary>Host-owned bounded cache, shared by pricing ingress, position monitoring and read-only queries.</summary>
public sealed class TradeRealtimeCacheRegistry
{
    readonly object gate = new();
    readonly ConcurrentDictionary<TradeEntityId, TradeRealtimeHotCache> trades = new();
    readonly ConcurrentDictionary<string, TradeRealtimeHotCache[]> publishedRoutes = new(StringComparer.Ordinal);
    TradeRealtimeHotCache[]? residentSnapshot;
    readonly Dictionary<string, HashSet<TradeEntityId>> routes = new(StringComparer.Ordinal);
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
            if (trades.Count >= 4096) throw new InvalidOperationException("Trade realtime cache capacity exceeded.");
            var cache = new TradeRealtimeHotCache(trade, date);
            trades[trade.Id] = cache;
            Volatile.Write(ref residentSnapshot, null);
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
    {
        using var mutation = TomasAI.IFM.Shared.EventModelActor.RealtimeActorGeneration.EnterMutation();
        var all = Volatile.Read(ref residentSnapshot);
        if (all is null)
            lock (gate)
            {
                all = residentSnapshot ?? trades.Values.ToArray();
                Volatile.Write(ref residentSnapshot, all);
            }
        List<TradeRealtimeSnapshot>? expired = null;
        foreach (var cache in all)
            if (cache.Expire()) (expired ??= []).Add(cache.ReadForPublication());
        return expired?.ToArray() ?? [];
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
        lock (gate)
        {
            if (!trades.TryRemove(id, out var cache)) return;
            cache.Retire();
            Volatile.Write(ref residentSnapshot, null);
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
