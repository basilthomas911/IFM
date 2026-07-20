using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

public interface ITradeDbWriteContext
{
    Task DeleteOptionTradeAsync(int orderId, int tradeId);
    Task DeleteTradeLimitAsync(int tradeId, TradeType tradeType);
    Task DeleteTradeTypeLimitAsync(int tradeId);
    Task DeleteTradeLiveFeedAsync(int orderId, int tradeId);
    Task DeleteTradeLiveFeedsAsync(int orderId);
    Task DeleteTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId id);
    Task DeleteTradePositionStateAsync(OptionTradeEntityId id);
    Task DeleteTradePositionAsync(int orderId, int tradeId);
    Task DeleteTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus);
    Task DeleteOptionTradeSpreadBarDataAsync(int orderId, int tradeId,  DateOnly valueDate, TradeType tradeType);
    Task DeleteOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    Task DeleteTradePlacementSignalAsync(string contractId, DateOnly valueDate);
    Task InsertOptionTradeAsync(OptionTradeReadModel optionTrade);
    Task InsertOptionTradesAsync(ICollection<OptionTradeReadModel> optionTrades);
    Task<long> InsertOptionTradesAsync(IEnumerable<OptionTradeReadModel> optionTrades);
    Task InsertTradePositionAsync(TradePositionReadModel tradePosition);
    Task InsertTradePositionAsync(ICollection<TradePositionReadModel> tradePositions);
    Task InsertTradePositionsAsync(ICollection<TradePositionReadModel> tradePositions);
    Task<long> InsertTradePositionsAsync(IEnumerable<TradePositionReadModel> tradePositions);
    Task InsertOptionLegAsync(OptionTradeLegReadModel optionLeg);
    Task InsertOptionLegsAsync(ICollection<OptionTradeLegReadModel> optionLegs);
    Task<long> InsertOptionLegsAsync(IEnumerable<OptionTradeLegReadModel> optionLegs);
    Task InsertOptionLegDataAsync(OptionTradeLegDataReadModel optionLegData);
    Task InsertOptionLegDataAsync(ICollection<OptionTradeLegDataReadModel> optionLegData);
    Task<long> InsertOptionLegDataAsync(IEnumerable<OptionTradeLegDataReadModel> optionLegData);
    Task InsertTradeLimitAsync(TradeLimitReadModel tradeLimit);
    Task InsertTradeLimitsAsync(ICollection<TradeLimitReadModel> tradeLimits);
    Task<long> InsertTradeLimitsAsync(IEnumerable<TradeLimitReadModel> tradeLimits);
    Task InsertTradeTypeLimitAsync(TradeTypeLimitReadModel tradeTypeLimit);
    Task InsertTradeTypeLimitsAsync(ICollection<TradeTypeLimitReadModel> tradeTypeLimits);
    Task<long>InsertTradeTypeLimitsAsync(IEnumerable<TradeTypeLimitReadModel> tradeTypeLimits);
    Task InsertTradeFillAsync(ICollection<TradeFillReadModel> tradeFills);
    Task InsertTradeFillsAsync(ICollection<TradeFillReadModel> tradeFills);
    Task<long> InsertTradeFillsAsync(IEnumerable<TradeFillReadModel> tradeFills);
    Task<long> InsertTradePlacementSignalAsync(TradePlacementSignalReadModel tradePlacementSignal);
    Task InsertTradePlanAsync(TradePlanReadModel tradePlan);
    Task InsertTradePlansAsync(ICollection<TradePlanByIdReadModel> tradePlans);
    Task<long> InsertTradePlansAsync(IEnumerable<TradePlanByIdReadModel> tradePlans);
    Task InsertTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel tradePlanForwardLossLimit);
    Task InsertTradePlanForwardLossRatioAsync(DateOnly valueDate, double forwardLosRatio);
    Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
    Task InsertTradeOrderAsync(TradeOrderReadModel tradeTicket);
    Task InsertTradePositionStateAsync(TradePositionStateReadModel tradePositionState);
    Task InsertOptionTradeSpreadDataAsync(OptionTradeSpreadsDataModel optionTradeSpreadData);
    Task InsertOptionTradeSpreadDataAsync(ICollection<OptionTradeSpreadsDataModel> optionTradeSpreadData);
    Task<long> InsertOptionTradeSpreadDataAsync(IEnumerable<OptionTradeSpreadsDataModel> optionTradeSpreadData);
    Task InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarsDataModel optionTradeSpreadBarData);
    Task InsertOptionTradeSpreadBarDataAsync(ICollection<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData);
    Task<long> InsertOptionTradeSpreadBarDataAsync(IEnumerable<OptionTradeSpreadBarsDataModel> optionTradeSpreadBarsData);

    Task UpdateOptionTradeStateAsync(int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy);
    Task UpdateTradePositionAsync(
        TradePositionEntityId key, 
        decimal commission,
        int deltaHedge,
        decimal netSpread,
        decimal tradeValue,
        decimal tradePnl,
        decimal assetPrice,
        double otmProbability,
        double winRatio,
        decimal maxPrice,
        double hedgeProbability,
        double riskFreeRate,
        DateTime updatedOn,
        string updatedBy);

    Task UpdateTradePositionStatusAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus oldTradeStatus,
        TradeStatus newTradeStatus,
        DateTime updatedOn,
        string updatedBy);

    Task UpdateOptionLegDataAsync(OptionTradeLegDataReadModel optionLegData);
    Task UpdateTradeLimitDailyProfitTarget(int tradeId, TradeType tradeType, decimal dailyProfitTarget, DateTime updatedOn, string updatedBy);
    Task UpdateTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
    Task UpdateTradeOrderStateAsync(TradeOrderEntityId tradeTicketId, TradeOrderState tradeOrderState, DateTime updatedOn, string updatedBy);
    Task UpdateTradeOrderOrderPriceAsync(TradeOrderEntityId tradeOrderId, decimal orderPrice, DateTime updatedOn, string updatedBy);

}
