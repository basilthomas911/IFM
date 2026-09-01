using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>
/// Owns the API process's immutable, monotonically versioned futures-session decision.
/// </summary>
public sealed class FuturesMarketSessionAuthority : IFuturesMarketSessionAuthority
{
    readonly object _gate = new();
    readonly TimeProvider _timeProvider;
    MarketSessionReadModel _current;

    public FuturesMarketSessionAuthority(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _current = CreateSnapshot(_timeProvider.GetUtcNow(), revision: 1);
    }

    /// <inheritdoc />
    public MarketSessionReadModel Current => Volatile.Read(ref _current);

    /// <summary>Re-evaluates the authoritative decision and advances its revision when state changes.</summary>
    public MarketSessionReadModel Refresh()
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            var candidate = GetMarketSession.Calculate(now);
            var current = _current;
            var revision = HasDecisionChanged(current, candidate)
                ? checked(current.Revision + 1)
                : current.Revision;
            var refreshed = candidate with
            {
                Revision = revision,
                AsOfUtc = now.UtcDateTime
            };
            Volatile.Write(ref _current, refreshed);
            return refreshed;
        }
    }

    MarketSessionReadModel CreateSnapshot(DateTimeOffset now, long revision)
        => GetMarketSession.Calculate(now) with
        {
            Revision = revision,
            AsOfUtc = now.UtcDateTime
        };

    static bool HasDecisionChanged(
        MarketSessionReadModel current,
        MarketSessionReadModel candidate)
        => current.OperationalValueDate != candidate.OperationalValueDate
           || current.ActiveValueDate != candidate.ActiveValueDate
           || current.State != candidate.State
           || current.SessionStartUtc != candidate.SessionStartUtc
           || current.SessionEndUtc != candidate.SessionEndUtc
           || current.NextTransitionUtc != candidate.NextTransitionUtc;
}
