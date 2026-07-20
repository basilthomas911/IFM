using System.Collections;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradeTypeLimitCollection(int tradeId) : ITradeTypeLimitCollection
{
    readonly int _tradeId = tradeId;
    readonly List<ITradeTypeLimit> _tradeTypeLimits = [];

    public int Count => _tradeTypeLimits.Count;
    
    public ITradeTypeLimit? this[TradeType tradeType]
        => _tradeTypeLimits
            .Where(e => e.TradeId == _tradeId && e.TradeType == tradeType)
            .SingleOrDefault();

    public bool Exists(TradeType tradeType) => _tradeTypeLimits.Any(e => e.TradeId == _tradeId && e.TradeType == tradeType);

    public void Add(ITradeTypeLimit item) => _tradeTypeLimits.Add(item);

    public void Clear() => _tradeTypeLimits.Clear();

    public IEnumerator<ITradeTypeLimit> GetEnumerator() => _tradeTypeLimits.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_tradeTypeLimits).GetEnumerator();

}
