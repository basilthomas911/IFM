using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public sealed class OptionChainStateStore : IOptionChainStateStore
{
    private readonly System.Threading.Lock _sync = new();
    private readonly Dictionary<OptionChainSessionKey,
        Dictionary<string, OptionChainContractState>> _sessions = [];

    internal void Create(
        OptionChainSessionKey key,
        IReadOnlyList<DatabentoOptionChainRoute> routes)
    {
        lock (_sync)
        {
            _sessions.Add(key, routes.ToDictionary(
                route => route.FuturesOptionContractId,
                route => new OptionChainContractState(route, null, null),
                StringComparer.Ordinal));
        }
    }

    internal void UpdateQuote(
        OptionChainSessionKey key,
        string contractId,
        LastQuoteTickWithGreeksSnapshot quote)
    {
        lock (_sync)
        {
            var current = _sessions[key][contractId];
            _sessions[key][contractId] = current with { Quote = quote };
        }
    }

    internal void UpdateTrade(OptionChainSessionKey key, string contractId, LastTradeTickWithGreeksSnapshot trade)
    {
        lock (_sync)
            _sessions[key][contractId] = _sessions[key][contractId] with { Trade = trade };
    }

    // Replay is counted separately from prices/callbacks. Its watermark survives physical connection expansion.
    internal void AccumulateVolume(OptionChainSessionKey key, string contractId, uint size, long eventNanoseconds,
        uint sequence, DateOnly valueDate)
    {
        lock (_sync)
        {
            var current = _sessions[key][contractId];
            if (eventNanoseconds < current.LastVolumeEventNanoseconds ||
                eventNanoseconds == current.LastVolumeEventNanoseconds && sequence <= current.LastVolumeSequence) return;
            var observed = checked(current.ObservedSessionVolume + size);
            var saved = current.VolumeValueDate == valueDate ? current.SessionVolume ?? 0 : 0;
            _sessions[key][contractId] = current with
            {
                ObservedSessionVolume = observed,
                LastVolumeEventNanoseconds = eventNanoseconds,
                LastVolumeSequence = sequence,
                SessionVolume = current.SessionVolumeOfficial && current.VolumeValueDate == valueDate ? saved :
                    current.VolumeReplayComplete ? observed : Math.Max(saved, observed),
                SessionVolumeOfficial = current.SessionVolumeOfficial && current.VolumeValueDate == valueDate,
                VolumeValueDate = valueDate
            };
        }
    }

    internal void CompleteVolumeReplay(OptionChainSessionKey key, string contractId, DateOnly valueDate)
    {
        lock (_sync)
        {
            var current = _sessions[key][contractId];
            _sessions[key][contractId] = current with
            {
                VolumeReplayComplete = true,
                SessionVolume = current.SessionVolumeOfficial && current.VolumeValueDate == valueDate
                    ? current.SessionVolume : current.ObservedSessionVolume,
                SessionVolumeOfficial = current.SessionVolumeOfficial && current.VolumeValueDate == valueDate,
                VolumeValueDate = valueDate
            };
        }
    }

    internal void Seed(OptionChainSessionKey key, IReadOnlyList<TomasAI.IFM.Framework.MarketData.Contracts.OptionChainStatisticsSnapshot> saved,
        DateOnly valueDate)
    {
        lock (_sync)
        {
            foreach (var value in saved)
            {
                if (!_sessions[key].TryGetValue(value.ContractId, out var current)) continue;
                if (value.Volume is >= 0 && value.VolumeValueDate is { } vd && vd <= valueDate &&
                    (current.VolumeValueDate is null || vd > current.VolumeValueDate))
                    current = current with { SessionVolume = value.Volume, VolumeValueDate = vd, SessionVolumeOfficial = false };
                if (value.OpenInterest is >= 0 && value.OpenInterestValueDate is { } od && od <= valueDate &&
                    (current.OpenInterestValueDate is null || od > current.OpenInterestValueDate))
                    current = current with { OpenInterest = value.OpenInterest, OpenInterestValueDate = od };
                _sessions[key][value.ContractId] = current;
            }
        }
    }

    internal void CopyStatistics(OptionChainSessionKey source, OptionChainSessionKey target, string contractId)
    {
        lock (_sync)
        {
            var sourceState = _sessions[source][contractId];
            var to = _sessions[target][contractId];
            _sessions[target][contractId] = sourceState with { Route = to.Route, Quote = to.Quote, Trade = null };
        }
    }

    internal void UpdateStatistics(OptionChainSessionKey key, string contractId,
        long? volume, long? openInterest, DateOnly referenceDate, DateTimeOffset observedAtUtc)
    {
        lock (_sync)
        {
            var current = _sessions[key][contractId];
            if (volume is >= 0 && Newer(referenceDate, observedAtUtc, current.VolumeValueDate, current.VolumeObservedAtUtc))
                current = current with { SessionVolume = volume, VolumeValueDate = referenceDate,
                    VolumeObservedAtUtc = observedAtUtc, SessionVolumeOfficial = true };
            if (openInterest is >= 0 && Newer(referenceDate, observedAtUtc, current.OpenInterestValueDate, current.OpenInterestObservedAtUtc))
                current = current with { OpenInterest = openInterest, OpenInterestValueDate = referenceDate, OpenInterestObservedAtUtc = observedAtUtc };
            if (current.StatisticsAtUtc is null || observedAtUtc > current.StatisticsAtUtc)
                current = current with { StatisticsAtUtc = observedAtUtc };
            _sessions[key][contractId] = current;
        }
    }

    static bool Newer(DateOnly date, DateTimeOffset observed, DateOnly? currentDate, DateTimeOffset? currentObserved) =>
        currentDate is null || date > currentDate || date == currentDate && (currentObserved is null || observed >= currentObserved);

    internal void Remove(OptionChainSessionKey key)
    {
        lock (_sync) _sessions.Remove(key);
    }

    /// <summary>Attempts to retrieve the specified option contract&apos;s session state.</summary>
    /// <param name="session">The session.</param>
    /// <param name="futuresOptionContractId">The futures option contract identifier.</param>
    /// <param name="state">The state returned by the operation.</param>
    /// <returns>True when the operation succeeds or the requested condition holds; otherwise, false.</returns>
    public bool TryGet(
        OptionChainSessionKey session,
        string futuresOptionContractId,
        out OptionChainContractState state)
    {
        lock (_sync)
        {
            if (_sessions.TryGetValue(session, out var contracts)
                && contracts.TryGetValue(futuresOptionContractId, out state))
                return true;
            state = default;
            return false;
        }
    }

    /// <inheritdoc />
    public int GetCount(OptionChainSessionKey session)
    {
        lock (_sync) return _sessions.TryGetValue(session, out var contracts) ? contracts.Count : 0;
    }

    /// <inheritdoc />
    public bool TryCopyContracts(OptionChainSessionKey session, IReadOnlyList<string> contractIds,
        Span<OptionChainContractState> destination)
    {
        if (destination.Length < contractIds.Count) throw new ArgumentException("Destination is too small.", nameof(destination));
        lock (_sync)
        {
            if (!_sessions.TryGetValue(session, out var contracts) || contracts.Count != contractIds.Count) return false;
            for (var index = 0; index < contractIds.Count; index++)
                if (!contracts.TryGetValue(contractIds[index], out destination[index])) return false;
            return true;
        }
    }

    /// <summary>Retrieves the option-chain state for the specified session.</summary>
    /// <param name="session">The session.</param>
    /// <returns>The session result.</returns>
    public IReadOnlyList<OptionChainContractState> GetSession(
        OptionChainSessionKey session)
    {
        lock (_sync)
        {
            return _sessions.TryGetValue(session, out var contracts)
                ? contracts.Values
                    .OrderBy(item => item.Route.Definition.StrikePrice)
                    .ThenBy(item => item.Route.Definition.Right)
                    .ThenBy(item => item.Route.FuturesOptionContractId, StringComparer.Ordinal)
                    .ToArray()
                : [];
        }
    }
}
