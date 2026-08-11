using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Framework.MarketData.DataBento.OptionChain;

public sealed class OptionChainStateStore : IOptionChainStateStore
{
    private readonly object _sync = new();
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

    internal void UpdateTrade(
        OptionChainSessionKey key,
        string contractId,
        LastTradeTickWithGreeksSnapshot trade)
    {
        lock (_sync)
        {
            var current = _sessions[key][contractId];
            _sessions[key][contractId] = current with { Trade = trade };
        }
    }

    internal void Remove(OptionChainSessionKey key)
    {
        lock (_sync) _sessions.Remove(key);
    }

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
