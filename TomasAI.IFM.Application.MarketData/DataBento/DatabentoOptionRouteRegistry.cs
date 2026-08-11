using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento;

internal sealed class DatabentoOptionRouteRegistry(int maximumChains)
{
    private readonly object _sync = new();
    private readonly Dictionary<string, string> _owners = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Underlying, DateOnly Maturity), string[]> _chains = [];

    internal bool StartIndividual(string optionContractId)
    {
        lock (_sync)
        {
            if (!_owners.TryGetValue(optionContractId, out var owner))
            {
                _owners.Add(optionContractId, "individual");
                return true;
            }
            if (owner == "individual") return false;
            throw new MarketDataRouteConflictException(optionContractId, owner);
        }
    }

    internal bool StopIndividual(string optionContractId)
    {
        lock (_sync)
        {
            return _owners.TryGetValue(optionContractId, out var owner)
                && owner == "individual"
                && _owners.Remove(optionContractId);
        }
    }

    internal bool ReserveChain(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds)
    {
        lock (_sync)
        {
            var key = (futuresContractId, maturityDate);
            var normalized = optionContractIds
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (_chains.TryGetValue(key, out var existing))
            {
                if (existing.SequenceEqual(normalized, StringComparer.Ordinal)) return false;
                throw new OptionChainConflictException(futuresContractId, maturityDate);
            }
            if (_chains.Count >= maximumChains)
                throw new MarketDataCapacityExceededException("option chains", maximumChains);
            foreach (var option in normalized)
            {
                if (_owners.TryGetValue(option, out var owner))
                    throw new MarketDataRouteConflictException(option, owner);
            }

            var chainOwner = $"chain:{futuresContractId}:{maturityDate:yyyy-MM-dd}";
            foreach (var option in normalized) _owners.Add(option, chainOwner);
            _chains.Add(key, normalized);
            return true;
        }
    }

    internal bool ReleaseChain(string futuresContractId, DateOnly maturityDate)
    {
        lock (_sync)
        {
            if (!_chains.Remove((futuresContractId, maturityDate), out var options))
                return false;
            foreach (var option in options) _owners.Remove(option);
            return true;
        }
    }

    internal void Clear()
    {
        lock (_sync)
        {
            _chains.Clear();
            _owners.Clear();
        }
    }
}
