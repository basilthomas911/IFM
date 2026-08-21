using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

/// <summary>Represents the downstream health of all currently traded futures feeds.</summary>
public enum MarketDataFeedHealthState
{
    Inactive,
    OutsidePositionEntryWindow,
    Healthy,
    Intermittent,
    Failed,
    Critical
}

/// <summary>One immutable evaluation of the currently traded feed set.</summary>
public sealed record MarketDataFeedHealthSnapshot(
    MarketDataFeedHealthState State,
    TimeSpan StaleDuration,
    IReadOnlyList<string> StaleContractIds,
    bool EnteredCritical);

/// <summary>
/// Tracks completed downstream bar updates for the current contracts and applies the
/// one-minute/yellow, five-minute/orange, and fifteen-minute/red escalation policy.
/// </summary>
internal sealed class MarketDataFeedHealthMonitor
{
    internal static readonly TimeSpan FreshnessLimit = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan IntermittentLimit = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan FailedLimit = TimeSpan.FromMinutes(15);

    readonly object _gate = new();
    readonly Dictionary<string, DateTimeOffset?> _lastUpdates = new(StringComparer.Ordinal);
    DateTimeOffset _activatedAtUtc;
    DateTimeOffset? _currentWindowStartUtc;
    bool _active;
    bool _criticalReported;

    public MarketDataFeedHealthSnapshot Activate(
        IEnumerable<string> requiredContractIds,
        DateTimeOffset utcNow)
    {
        ArgumentNullException.ThrowIfNull(requiredContractIds);
        lock (_gate)
        {
            _lastUpdates.Clear();
            foreach (var contractId in requiredContractIds
                         .Where(value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.Ordinal))
            {
                _lastUpdates[contractId] = null;
            }
            if (_lastUpdates.Count == 0)
                _lastUpdates["No currently traded contracts configured"] = null;

            _activatedAtUtc = utcNow;
            _currentWindowStartUtc = null;
            _active = true;
            _criticalReported = false;
            return EvaluateCore(utcNow);
        }
    }

    public MarketDataFeedHealthSnapshot Deactivate()
    {
        lock (_gate)
        {
            _active = false;
            _criticalReported = false;
            _currentWindowStartUtc = null;
            _lastUpdates.Clear();
            return Snapshot(MarketDataFeedHealthState.Inactive);
        }
    }

    public MarketDataFeedHealthSnapshot RecordUpdate(string contractId, DateTimeOffset utcNow)
    {
        lock (_gate)
        {
            if (_active && _lastUpdates.ContainsKey(contractId))
                _lastUpdates[contractId] = utcNow;
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
        if (!_active)
            return Snapshot(MarketDataFeedHealthState.Inactive);

        var windowStartUtc = PositionEntryWindow.GetCurrentStartUtc(utcNow);
        if (!windowStartUtc.HasValue)
        {
            _currentWindowStartUtc = null;
            _criticalReported = false;
            return Snapshot(MarketDataFeedHealthState.OutsidePositionEntryWindow);
        }

        if (_currentWindowStartUtc != windowStartUtc)
        {
            _currentWindowStartUtc = windowStartUtc;
            _criticalReported = false;
        }

        var monitoringBaseline = Latest(_activatedAtUtc, windowStartUtc.Value);
        var staleContracts = new List<string>();
        var maximumAge = TimeSpan.Zero;
        foreach (var (contractId, lastUpdateUtc) in _lastUpdates)
        {
            var freshnessReference = lastUpdateUtc.HasValue
                ? Latest(monitoringBaseline, lastUpdateUtc.Value)
                : monitoringBaseline;
            var age = utcNow - freshnessReference;
            if (age <= FreshnessLimit)
                continue;

            staleContracts.Add(contractId);
            if (age > maximumAge)
                maximumAge = age;
        }

        if (staleContracts.Count == 0)
        {
            _criticalReported = false;
            return Snapshot(MarketDataFeedHealthState.Healthy);
        }

        var staleDuration = maximumAge - FreshnessLimit;
        var state = staleDuration < IntermittentLimit
            ? MarketDataFeedHealthState.Intermittent
            : staleDuration < IntermittentLimit + FailedLimit
                ? MarketDataFeedHealthState.Failed
                : MarketDataFeedHealthState.Critical;
        var enteredCritical = state == MarketDataFeedHealthState.Critical && !_criticalReported;
        _criticalReported = state == MarketDataFeedHealthState.Critical;
        return new MarketDataFeedHealthSnapshot(state, staleDuration, staleContracts, enteredCritical);
    }

    static DateTimeOffset Latest(DateTimeOffset left, DateTimeOffset right)
        => left >= right ? left : right;

    static MarketDataFeedHealthSnapshot Snapshot(MarketDataFeedHealthState state)
        => new(state, TimeSpan.Zero, [], false);
}
