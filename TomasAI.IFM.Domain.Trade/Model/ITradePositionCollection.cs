using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradePositionCollection : IEnumerable<ITradePosition>
{
    int Count { get; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1043", Justification = "The composite position identity is the published lookup key; converting it to text would lose type safety.")]
    ITradePosition? this[TradePositionEntityId key] { get; }
    ITradePosition[] Opening();
    ITradePosition? Opening(TradeType tradeType);
    ITradePosition? IntraDay(TradeType tradeType);
    ITradePosition? IntraDay(TradeType tradeType, DateOnly valueDate);
    ITradePosition? EndOfDay(TradeType tradeType);
    ITradePosition? EndOfDay(TradeType tradeType, DateOnly valueDate);
    ITradePosition? Closing(TradeType tradeType);
    TradePositionChangeSourceType Source(TradeType tradeType);
    bool Exists(TradePositionEntityId key);
    void Add(ITradePosition spreadTradeData);
    void Clear();
    bool Remove(ITradePosition spreadTradeData);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Preserve the existing public interface member used by providers and actor callers.")]
    ITradePosition New(TradePositionEntityId tradePositionKey, DateTime createdOn, string createdBy);
    ITradePositionCollection SetTradePnl(TradeType tradeType);
    ITradePositionCollection SetClosingTradePnl(TradeType tradeType, decimal openingTradeValue);
}
