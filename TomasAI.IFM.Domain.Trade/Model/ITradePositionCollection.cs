using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradePositionCollection : IEnumerable<ITradePosition>
{
    int Count { get; }
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
    ITradePosition New(TradePositionEntityId tradePositionKey, DateTime createdOn, string createdBy);
    ITradePositionCollection SetTradePnl(TradeType tradeType);
    ITradePositionCollection SetClosingTradePnl(TradeType tradeType, decimal openingTradeValue);
}
