using System.Collections;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.Domain.Fund.Command.Model;

/// <summary>
/// fund order trade collection
/// </summary>
public class FundOrderTradeCollection : IFundOrderTradeCollection
{
    readonly Dictionary<int, IFundOrderTrade> _fundOrderTrades;

    /// <summary>
    /// fund order trade collection constructor
    /// </summary>
    public FundOrderTradeCollection() 
        => _fundOrderTrades = [];

    /// <summary>
    /// return selected fund order trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public IFundOrderTrade? this[int tradeId]
        => _fundOrderTrades.TryGetValue(tradeId, out var trade) ? trade : null;
    
    /// <summary>
    /// return count of fund order trades
    /// </summary>
    public int Count 
        => _fundOrderTrades.Count;

    /// <summary>
    /// check if fund order trade exists in collection
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public bool Exists(int tradeId) 
        => _fundOrderTrades.ContainsKey(tradeId);

    public bool TryGet(int tradeId, out IFundOrderTrade? trade)
        => _fundOrderTrades.TryGetValue(tradeId, out trade);

    /// <summary>
    /// add fund order trade to collection
    /// </summary>
    /// <param name="item"></param>
    public void Add(IFundOrderTrade item)
        => _fundOrderTrades[item.TradeId] = item;

    /// <summary>
    /// add fund order trades to collection
    /// </summary>
    /// <param name="items"></param>
    public void AddRange(IEnumerable<IFundOrderTrade> items)
    {
        foreach (var item in items)
            Add(item);
    }

    /// <summary>
    /// remove fund order trade from collection
    /// </summary>
    /// <param name="item"></param>
    public void Remove(IFundOrderTrade item) 
        => _fundOrderTrades.Remove(item.TradeId);

    public bool Remove(int tradeId)
        => _fundOrderTrades.Remove(tradeId);

    /// <summary>
    /// return collection iterator
    /// </summary>
    /// <returns></returns>
    public IEnumerator<IFundOrderTrade> GetEnumerator() 
        => _fundOrderTrades.Values.GetEnumerator();

    /// <summary>
    /// return collection iterator
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() 
        => GetEnumerator();
}
