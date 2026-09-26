using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Futures.Realtime.Model;

/// <summary>
/// Mailbox-owned one-to-many ContractId route index. Reads reuse stable route arrays;
/// lifecycle mutations use copy-on-write because open/close changes are rare.
/// </summary>
public sealed class ContractIdRouteIndex
{
    readonly Dictionary<string, PortfolioFundTradeLeg[]> _routes;
    readonly HashSet<string> _knownContracts;
    readonly Dictionary<string, long> _lastSourceSequences;
    readonly Dictionary<Guid, long> _positionGenerations;
    readonly HashSet<ReportedOutcome> _reportedOutcomes;

    public ContractIdRouteIndex(int capacity = 1024)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _routes = new Dictionary<string, PortfolioFundTradeLeg[]>(capacity, StringComparer.Ordinal);
        _knownContracts = new HashSet<string>(capacity, StringComparer.Ordinal);
        _lastSourceSequences = new Dictionary<string, long>(capacity, StringComparer.Ordinal);
        _positionGenerations = new Dictionary<Guid, long>(capacity);
        _reportedOutcomes = new HashSet<ReportedOutcome>(capacity);
    }

    public long ReceivedTicks { get; private set; }
    public long RoutedTicks { get; private set; }
    public long UnroutedTicks { get; private set; }
    public long StaleTicks { get; private set; }
    public long InvalidTicks { get; private set; }
    public long DuplicateOrOutOfOrderTicks { get; private set; }

    public void RegisterKnownContract(string contractId)
    {
        if (!string.IsNullOrWhiteSpace(contractId)) _knownContracts.Add(contractId);
    }

    public bool Add(PortfolioFundTradeLeg route, string contractId)
    {
        if (string.IsNullOrWhiteSpace(contractId) || route.PortfolioId <= 0 || route.FundId <= 0 ||
            route.OrderId <= 0 || route.TradeId <= 0 || route.StrategyPositionId == Guid.Empty ||
            route.TradeLegId == Guid.Empty || route.TradeType == TradeStrategyKind.Unknown || route.Generation <= 0)
            return false;

        if (_positionGenerations.TryGetValue(route.StrategyPositionId, out var currentGeneration))
        {
            if (route.Generation < currentGeneration) return false;
            if (route.Generation > currentGeneration) RemovePositionCore(route.StrategyPositionId);
        }
        _positionGenerations[route.StrategyPositionId] = route.Generation;

        _knownContracts.Add(contractId);
        ResetReportedOutcomes(contractId);
        if (!_routes.TryGetValue(contractId, out var existing))
        {
            _routes.Add(contractId, [route]);
            return true;
        }

        for (var index = 0; index < existing.Length; index++)
        {
            ref readonly var current = ref existing[index];
            if (current.StrategyPositionId != route.StrategyPositionId || current.TradeLegId != route.TradeLegId)
                continue;
            if (current == route) return false;
            var replaced = (PortfolioFundTradeLeg[])existing.Clone();
            replaced[index] = route;
            _routes[contractId] = replaced;
            return true;
        }

        var expanded = new PortfolioFundTradeLeg[existing.Length + 1];
        existing.CopyTo(expanded, 0);
        expanded[^1] = route;
        _routes[contractId] = expanded;
        return true;
    }

    public int RemovePosition(Guid strategyPositionId, long generation)
    {
        if (strategyPositionId == Guid.Empty || generation <= 0) return 0;
        if (_positionGenerations.TryGetValue(strategyPositionId, out var currentGeneration)
            && generation <= currentGeneration) return 0;
        _positionGenerations[strategyPositionId] = generation;
        return RemovePositionCore(strategyPositionId);
    }

    int RemovePositionCore(Guid strategyPositionId)
    {
        var removed = 0;
        foreach (var pair in _routes.ToArray())
        {
            var count = pair.Value.Count(route => route.StrategyPositionId == strategyPositionId);
            if (count == 0) continue;
            removed += count;
            if (count == pair.Value.Length)
                _routes.Remove(pair.Key);
            else
                _routes[pair.Key] = pair.Value.Where(route => route.StrategyPositionId != strategyPositionId).ToArray();
            ResetReportedOutcomes(pair.Key);
        }
        return removed;
    }

    public void ReplaceFromSnapshot(IEnumerable<OpenPositionRouteReadModel> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        _routes.Clear();
        _lastSourceSequences.Clear();
        _positionGenerations.Clear();
        _reportedOutcomes.Clear();
        foreach (var item in routes) Add(item.Route, item.ContractId);
    }

    /// <summary>Classifies a tick and returns the stable route array without allocating.</summary>
    public MarketRouteLookupOutcome TryRoute(in PositionMarketTick tick, out PortfolioFundTradeLeg[] routes)
    {
        ReceivedTicks++;
        routes = [];
        if (string.IsNullOrWhiteSpace(tick.ContractId) || tick.Price <= 0 || tick.SourceSequence <= 0 ||
            tick.OccurredAtUtc.Kind != DateTimeKind.Utc)
        {
            InvalidTicks++;
            return MarketRouteLookupOutcome.InvalidTick;
        }
        if (_lastSourceSequences.TryGetValue(tick.ContractId, out var last) && tick.SourceSequence <= last)
        {
            DuplicateOrOutOfOrderTicks++;
            return MarketRouteLookupOutcome.DuplicateOrOutOfOrder;
        }
        _lastSourceSequences[tick.ContractId] = tick.SourceSequence;
        if (!_routes.TryGetValue(tick.ContractId, out routes!))
        {
            UnroutedTicks++;
            return _knownContracts.Contains(tick.ContractId)
                ? MarketRouteLookupOutcome.NoOpenPosition
                : MarketRouteLookupOutcome.UnknownInstrument;
        }
        RoutedTicks++;
        return MarketRouteLookupOutcome.Routed;
    }

    public bool TryGetRoutes(string contractId, out PortfolioFundTradeLeg[] routes) =>
        _routes.TryGetValue(contractId, out routes!);

    /// <summary>Returns true once for each contract/outcome pair until route state changes.</summary>
    public bool ShouldLog(string contractId, MarketRouteLookupOutcome outcome) =>
        _reportedOutcomes.Add(new ReportedOutcome(contractId, outcome));

    void ResetReportedOutcomes(string contractId)
    {
        _reportedOutcomes.Remove(new ReportedOutcome(contractId, MarketRouteLookupOutcome.NoOpenPosition));
        _reportedOutcomes.Remove(new ReportedOutcome(contractId, MarketRouteLookupOutcome.UnknownInstrument));
    }

    readonly record struct ReportedOutcome(string ContractId, MarketRouteLookupOutcome Outcome);
}
