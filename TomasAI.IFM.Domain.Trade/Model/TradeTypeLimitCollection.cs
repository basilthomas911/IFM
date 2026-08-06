using System.Collections;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradeTypeLimitCollection(int tradeId) : ITradeTypeLimitCollection
{
    readonly int _tradeId = tradeId;
    readonly List<ITradeTypeLimit> _tradeTypeLimits = [];

    public int Count => _tradeTypeLimits.Count;
    
    public ITradeTypeLimit? this[TradeType tradeType]
    {
        get
        {
            ITradeTypeLimit? result = null;
            foreach (var tradeTypeLimit in _tradeTypeLimits)
            {
                if (tradeTypeLimit.TradeId != _tradeId || tradeTypeLimit.TradeType != tradeType)
                    continue;
                if (result is not null)
                    throw new InvalidOperationException("Sequence contains more than one matching element");
                result = tradeTypeLimit;
            }
            return result;
        }
    }

    public bool Exists(TradeType tradeType) => this[tradeType] is not null;

    public void Add(ITradeTypeLimit item) => _tradeTypeLimits.Add(item);

    public void Clear() => _tradeTypeLimits.Clear();

    public IEnumerator<ITradeTypeLimit> GetEnumerator() => _tradeTypeLimits.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_tradeTypeLimits).GetEnumerator();

}
