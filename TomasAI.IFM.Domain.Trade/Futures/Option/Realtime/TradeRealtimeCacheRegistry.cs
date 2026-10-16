using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;

/// <summary>Host-owned bounded cache, shared by pricing ingress, position monitoring and read-only queries.</summary>
public sealed class TradeRealtimeCacheRegistry
{
    readonly object gate = new();
    readonly Dictionary<TradeEntityId, TradeRealtimeHotCache> trades = [];
    readonly Dictionary<string, HashSet<TradeEntityId>> routes = new(StringComparer.Ordinal);
    public TradeRealtimeHotCache Register(EstablishedTradeDefinition trade, DateOnly date)
    {
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
            trades.Add(trade.Id, cache);
            foreach (var leg in trade.Legs)
            {
                if (!routes.TryGetValue(leg.ContractId, out var ids)) routes[leg.ContractId] = ids = [];
                ids.Add(trade.Id);
            }
            return cache;
        }
    }
    public TradeRealtimeHotCache? Find(TradeEntityId id)
    {
        lock (gate) return trades.GetValueOrDefault(id);
    }
    public TradeRealtimeSnapshot[] Expire()
    {
        TradeRealtimeHotCache[] all;
        lock (gate) all = trades.Values.ToArray();
        return all.Where(cache => cache.Expire()).Select(cache => cache.Read()).ToArray();
    }
    public TradeRealtimeSnapshot[] Apply(FuturesOptionTickDataV2ReadModel observation)
    {
        TradeRealtimeHotCache[] interested;
        lock (gate) interested = routes.TryGetValue(observation.ContractId, out var ids)
            ? ids.Select(id => trades[id]).ToArray() : [];
        return interested.Where(cache => cache.Apply(observation)).Select(cache => cache.Read()).ToArray();
    }
    public void Remove(TradeEntityId id)
    {
        lock (gate)
        {
            if (!trades.Remove(id, out var cache)) return;
            cache.Retire();
            foreach (var leg in cache.Read().Legs)
                if (routes.TryGetValue(leg.Definition.ContractId, out var ids))
                {
                    ids.Remove(id);
                    if (ids.Count == 0) routes.Remove(leg.Definition.ContractId);
                }
        }
    }
}
