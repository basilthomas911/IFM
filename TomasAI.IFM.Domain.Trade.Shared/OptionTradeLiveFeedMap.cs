using TomasAI.IFM.Shared.Trade;
using System.Collections.Concurrent;
using TomasAI.IFM.Domain.Trade.Shared.Contracts;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared;

public class OptionTradeLiveFeedMap
    : ConcurrentDictionary<OptionTradeEntityId, OptionTradeReadModel>, IOptionTradeLiveFeedMap
{
    public OptionTradeReadModel[] this[string optionLegContractId]
        => GetOptionTradeByOptionLegContractId(optionLegContractId);

    public bool Exists(OptionTradeEntityId key) 
        => ContainsKey(key);

    public void Add(OptionTradeReadModel optionTrade)
    {
        var key = new OptionTradeEntityId(optionTrade.OrderId, optionTrade.TradeId);
        if (!TryAdd(key, optionTrade))
        {
            TryRemove(key, out _);
            TryAdd(key, optionTrade);
        }
    }

    public bool Remove(OptionTradeReadModel optionTrade)
    {
        var key = new OptionTradeEntityId(optionTrade.OrderId, optionTrade.TradeId);
        return TryRemove(key, out _);
    }

    OptionTradeReadModel[] GetOptionTradeByOptionLegContractId(string optionLegContractId)
    {
        List<OptionTradeReadModel> optionTrades = [];

        return [.. optionTrades];
    }
}
