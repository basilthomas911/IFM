using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

/// <summary>Session-aware accepted-input health of explicitly enabled Databento routes.</summary>
public enum MarketDataFeedHealthState
{
    Inactive,
    OffHoursActive,
    OffHoursDegraded,
    Healthy,
    Intermittent,
    Critical
}

public sealed record MarketDataFeedHealthSnapshot(
    MarketDataFeedHealthState State,
    TimeSpan StaleDuration,
    IReadOnlyList<string> StaleContractIds,
    bool EnteredCritical);

/// <summary>
/// Uses 5/15-minute green/yellow/red health during live trading and one
/// non-critical fifteen-minute degraded state during off-trading hours.
/// </summary>
internal sealed class MarketDataFeedHealthMonitor
{
    internal static readonly TimeSpan FreshnessLimit = MarketDataFeedSessionHealthPolicy.GreenLimit;
    internal static readonly TimeSpan DegradedLimit = MarketDataFeedSessionHealthPolicy.DegradedLimit;

    readonly object _gate = new();
    readonly Dictionary<string, DateTimeOffset?> _lastUpdates = new(StringComparer.Ordinal);
    DateTimeOffset _activatedAtUtc;
    DateTimeOffset _liveBaselineUtc;
    FuturesMarketState _marketState = FuturesMarketState.Closed;
    bool _active;
    bool _criticalReported;

    public MarketDataFeedHealthSnapshot Activate(
        IEnumerable<string> requiredContractIds,
        DateTimeOffset utcNow,
        FuturesMarketState marketState)
    {
        ArgumentNullException.ThrowIfNull(requiredContractIds);
        lock (_gate)
        {
            _lastUpdates.Clear();
            foreach (var contractId in requiredContractIds
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.Ordinal))
                _lastUpdates[contractId] = null;
            if (_lastUpdates.Count == 0)
                _lastUpdates["No currently traded contracts configured"] = null;

            _activatedAtUtc = utcNow;
            _liveBaselineUtc = utcNow;
            _marketState = marketState;
            _active = true;
            _criticalReported = false;
            return EvaluateCore(utcNow);
        }
    }

    public MarketDataFeedHealthSnapshot SetMarketState(
        FuturesMarketState marketState,
        DateTimeOffset utcNow)
    {
        lock (_gate)
        {
            if (_marketState != FuturesMarketState.LiveTrading
                && marketState == FuturesMarketState.LiveTrading)
                _liveBaselineUtc = utcNow;
            if (marketState != FuturesMarketState.LiveTrading)
                _criticalReported = false;
            _marketState = marketState;
            return EvaluateCore(utcNow);
        }
    }

    public MarketDataFeedHealthSnapshot Deactivate()
    {
        lock (_gate)
        {
            _active = false;
            _criticalReported = false;
            _marketState = FuturesMarketState.Closed;
            _lastUpdates.Clear();
            return Snapshot(MarketDataFeedHealthState.Inactive);
        }
    }

    public MarketDataFeedHealthSnapshot RecordUpdate(
        string contractId,
        DateTimeOffset utcNow,
        DateTimeOffset? sourceEventUtc = null)
    {
        lock (_gate)
        {
            if (_active && _lastUpdates.ContainsKey(contractId))
                _lastUpdates[contractId] = sourceEventUtc is { } source
                    && source != default
                    && source < utcNow
                        ? source
                        : utcNow;
            return EvaluateCore(utcNow);
        }
    }

    public MarketDataFeedHealthSnapshot Evaluate(DateTimeOffset utcNow)
    {
        lock (_gate)
            return EvaluateCore(utcNow);
    }

    MarketDataFeedHealthSnapshot EvaluateCore(DateTimeOffset utcNow)
    {
        if (!_active || _marketState == FuturesMarketState.Closed)
            return Snapshot(MarketDataFeedHealthState.Inactive);

        var maximumAge = TimeSpan.Zero;
        var ages = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);
        foreach (var (contractId, lastUpdateUtc) in _lastUpdates)
        {
            var reference = lastUpdateUtc is { } update && update > _activatedAtUtc
                ? update
                : _activatedAtUtc;
            if (_marketState == FuturesMarketState.LiveTrading && reference < _liveBaselineUtc)
                reference = _liveBaselineUtc;
            var age = utcNow <= reference ? TimeSpan.Zero : utcNow - reference;
            ages[contractId] = age;
            if (age > maximumAge)
                maximumAge = age;
        }

        if (_marketState == FuturesMarketState.OffTrading)
        {
            _criticalReported = false;
            if (maximumAge <= DegradedLimit)
                return Snapshot(MarketDataFeedHealthState.OffHoursActive);
            var stale = ages.Where(pair => pair.Value > DegradedLimit)
                .Select(pair => pair.Key)
                .ToArray();
            return new MarketDataFeedHealthSnapshot(
                MarketDataFeedHealthState.OffHoursDegraded,
                maximumAge - DegradedLimit,
                stale,
                false);
        }

        if (maximumAge <= FreshnessLimit)
        {
            _criticalReported = false;
            return Snapshot(MarketDataFeedHealthState.Healthy);
        }

        var state = maximumAge <= DegradedLimit
            ? MarketDataFeedHealthState.Intermittent
            : MarketDataFeedHealthState.Critical;
        var threshold = state == MarketDataFeedHealthState.Intermittent
            ? FreshnessLimit
            : DegradedLimit;
        var staleContracts = ages.Where(pair => pair.Value > threshold)
            .Select(pair => pair.Key)
            .ToArray();
        var enteredCritical = state == MarketDataFeedHealthState.Critical && !_criticalReported;
        _criticalReported = state == MarketDataFeedHealthState.Critical;
        return new MarketDataFeedHealthSnapshot(
            state,
            maximumAge - threshold,
            staleContracts,
            enteredCritical);
    }

    static MarketDataFeedHealthSnapshot Snapshot(MarketDataFeedHealthState state)
        => new(state, TimeSpan.Zero, [], false);
}
