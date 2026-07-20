using System.Collections;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.Domain.Fund.Command.Model;

/// <summary>
/// fund order trade collection
/// </summary>
public class FundOrderTradeCollection : IFundOrderTradeCollection
{
    List<IFundOrderTrade> _fundOrderTrades;

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
        => _fundOrderTrades?.Where(e => e.TradeId == tradeId)?.SingleOrDefault();
    
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
        => _fundOrderTrades.Exists(e => e.TradeId == tradeId);

    /// <summary>
    /// add fund order trade to collection
    /// </summary>
    /// <param name="item"></param>
    public void Add(IFundOrderTrade item)
        => _fundOrderTrades.Add(item);

    /// <summary>
    /// add fund order trades to collection
    /// </summary>
    /// <param name="items"></param>
    public void AddRange(IEnumerable<IFundOrderTrade> items) 
        => _fundOrderTrades.AddRange(items);

    /// <summary>
    /// remove fund order trade from collection
    /// </summary>
    /// <param name="item"></param>
    public void Remove(IFundOrderTrade item) 
        => _fundOrderTrades.Remove(item);

    /// <summary>
    /// return collection iterator
    /// </summary>
    /// <returns></returns>
    public IEnumerator<IFundOrderTrade> GetEnumerator() 
        => _fundOrderTrades.GetEnumerator();

    /// <summary>
    /// return collection iterator
    /// </summary>
    IEnumerator IEnumerable.GetEnumerator() 
        => ((IEnumerable)_fundOrderTrades).GetEnumerator();
}
