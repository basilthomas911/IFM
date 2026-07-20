using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradeTypeLimitCollection : IEnumerable<ITradeTypeLimit>
{
    int Count { get; }
    ITradeTypeLimit? this[TradeType tradeType] { get; }
    bool Exists(TradeType tradeType);
    void Add(ITradeTypeLimit item);
    void Clear();
}
