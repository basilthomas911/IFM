using TomasAI.IFM.Domain.Trade.Shared.Model;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;

/// <summary>
/// Mailbox-owned one-to-many reverse index. Read operations allocate no managed objects.
/// Route mutations are deliberately copy-on-write because position lifecycle changes are rare.
/// </summary>
public sealed class MarketInstrumentRouteIndex
{
    readonly Dictionary<uint, MarketPositionRoute[]> _routes;
    readonly HashSet<uint> _knownInstruments;
    readonly Dictionary<uint, long> _lastSourceSequences;
    readonly HashSet<ulong> _reportedOutcomes;

    public MarketInstrumentRouteIndex(int capacity = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _routes = new Dictionary<uint, MarketPositionRoute[]>(capacity);
        _knownInstruments = new HashSet<uint>(capacity);
        _lastSourceSequences = new Dictionary<uint, long>(capacity);
        _reportedOutcomes = new HashSet<ulong>(capacity);
    }

    public long ReceivedTicks { get; private set; }
    public long RoutedTicks { get; private set; }
    public long UnroutedTicks { get; private set; }
    public long StaleTicks { get; private set; }
    public long InvalidTicks { get; private set; }
    public long DuplicateOrOutOfOrderTicks { get; private set; }

    public void RegisterKnownInstrument(uint marketInstrumentId)
    {
        if (marketInstrumentId != 0) _knownInstruments.Add(marketInstrumentId);
    }

    public bool Add(MarketPositionRoute route, uint marketInstrumentId)
    {
        if (marketInstrumentId == 0 || route.StrategyPositionId == Guid.Empty || route.TradeLegId == Guid.Empty || route.Generation <= 0)
            return false;
        _knownInstruments.Add(marketInstrumentId);
        ResetReportedOutcomes(marketInstrumentId);
        if (!_routes.TryGetValue(marketInstrumentId, out var existing))
        {
            _routes.Add(marketInstrumentId, [route]);
            return true;
        }
        for (var index = 0; index < existing.Length; index++)
        {
            ref readonly var current = ref existing[index];
            if (current.StrategyPositionId != route.StrategyPositionId || current.TradeLegId != route.TradeLegId) continue;
            if (current == route) return false;
            var replaced = (MarketPositionRoute[])existing.Clone();
            replaced[index] = route;
            _routes[marketInstrumentId] = replaced;
            return true;
        }
        var expanded = new MarketPositionRoute[existing.Length + 1];
        existing.CopyTo(expanded, 0);
        expanded[^1] = route;
        _routes[marketInstrumentId] = expanded;
        return true;
    }

    public int RemovePosition(Guid strategyPositionId)
    {
        var removed = 0;
        foreach (var pair in _routes.ToArray())
        {
            var count = 0;
            for (var index = 0; index < pair.Value.Length; index++)
                if (pair.Value[index].StrategyPositionId == strategyPositionId) count++;
            if (count == 0) continue;
            removed += count;
            if (count == pair.Value.Length) _routes.Remove(pair.Key);
            else
            {
                var retained = new MarketPositionRoute[pair.Value.Length - count];
                var target = 0;
                for (var index = 0; index < pair.Value.Length; index++)
                    if (pair.Value[index].StrategyPositionId != strategyPositionId) retained[target++] = pair.Value[index];
                _routes[pair.Key] = retained;
            }
            ResetReportedOutcomes(pair.Key);
        }
        return removed;
    }

    public void ReplaceFromSnapshot(IEnumerable<(uint InstrumentId, MarketPositionRoute Route)> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        _routes.Clear();
        _lastSourceSequences.Clear();
        _reportedOutcomes.Clear();
        foreach (var item in routes) Add(item.Route, item.InstrumentId);
    }

    /// <summary>Classifies a tick and returns the stable route array without allocating.</summary>
    public MarketRouteLookupOutcome TryRoute(in PositionMarketTick tick, out MarketPositionRoute[] routes)
    {
        ReceivedTicks++;
        routes = [];
        if (tick.MarketInstrumentId == 0 || tick.Price <= 0 || tick.SourceSequence <= 0 || tick.OccurredAtUtc.Kind != DateTimeKind.Utc)
        {
            InvalidTicks++;
            return MarketRouteLookupOutcome.InvalidTick;
        }
        if (_lastSourceSequences.TryGetValue(tick.MarketInstrumentId, out var last) && tick.SourceSequence <= last)
        {
            DuplicateOrOutOfOrderTicks++;
            return MarketRouteLookupOutcome.DuplicateOrOutOfOrder;
        }
        _lastSourceSequences[tick.MarketInstrumentId] = tick.SourceSequence;
        if (!_routes.TryGetValue(tick.MarketInstrumentId, out routes!))
        {
            UnroutedTicks++;
            return _knownInstruments.Contains(tick.MarketInstrumentId)
                ? MarketRouteLookupOutcome.NoOpenPosition
                : MarketRouteLookupOutcome.UnknownInstrument;
        }
        RoutedTicks++;
        return MarketRouteLookupOutcome.Routed;
    }

    public bool TryGetRoutes(uint marketInstrumentId, out MarketPositionRoute[] routes) =>
        _routes.TryGetValue(marketInstrumentId, out routes!);

    /// <summary>Returns true once for each instrument/outcome pair until its route state changes.</summary>
    public bool ShouldLog(uint marketInstrumentId, MarketRouteLookupOutcome outcome) =>
        _reportedOutcomes.Add(((ulong)outcome << 32) | marketInstrumentId);

    void ResetReportedOutcomes(uint marketInstrumentId)
    {
        _reportedOutcomes.Remove(((ulong)MarketRouteLookupOutcome.NoOpenPosition << 32) | marketInstrumentId);
        _reportedOutcomes.Remove(((ulong)MarketRouteLookupOutcome.UnknownInstrument << 32) | marketInstrumentId);
    }
}
