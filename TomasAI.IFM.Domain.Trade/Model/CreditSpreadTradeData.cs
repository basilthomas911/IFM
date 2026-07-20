using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Trade.Model;

public class CreditSpreadTradeData(
    int orderId,
    int tradeId,
    TradeType tradeType,
    DateOnly valueDate,
    int daysToExpiry,
    TradeStatus tradeStatus,
    DateTime createdOn,
    string createdBy) : TradePosition(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, createdOn, createdBy)
{
}
