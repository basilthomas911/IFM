using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeDb
{
    public interface ITradeDbWriteContext : ITradeDbContext
    {
        Task DeleteOptionTradeAsync(int tradeId);
        Task DeleteTradeTypeLimitAsync(int tradeId, TradeType tradeType);
        Task DeleteTradeLiveFeedAsync(int orderId, int tradeId);
        Task DeleteTradeLiveFeedsAsync(int orderId);
        Task DeleteTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitId id);
        Task DeleteTradePositionStateAsync(OptionTradeId id);
        Task DeleteOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate);
        Task InsertOptionTradeAsync(OptionTradeViewModel optionTrade);
        Task InsertTradePositionAsync(TradePositionReadModel tradePosition);
        Task InsertTradePositionsAsync(ICollection<TradePositionReadModel> tradePositions);
        Task InsertOptionLegAsync(OptionLegReadModel optionLeg);
        Task InsertOptionLegDataAsync(OptionLegDataReadModel optionLegData);
        Task InsertTradeLimitAsync(TradeLimitReadModel tradeLimit);
        Task InsertTradeTypeLimitAsync(TradeTypeLimitReadModel tradeTypeLimit);
        Task InsertTradeFillsAsync(ICollection<TradeFillReadModel> tradeFills);
        Task InsertTradePlacementSignalAsync(TradePlacementSignalReadModel tradePlacementSignal);
        Task InsertTradePlanAsync(TradePlanReadModel tradePlan);
        Task InsertTradePlanActionAsync(TradePlanActionReadModel tradePlanSummary);
        Task InsertTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel tradePlanForwardLossLimit);
        Task InsertTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
        Task InsertTradeOrderAsync(TradeOrderReadModel tradeTicket);
        Task InsertTradeDiaryAsync(TradeDiaryEntryReadModel tradeDiaryEntry);
        Task InsertTradePositionStateAsync(TradePositionStateReadModel tradePositionState);
        Task InsertOptionTradeSpreadDataAsync(OptionTradeSpreadDataViewModel optionTradeSpreadData);
        Task InsertOptionTradeSpreadBarDataAsync(OptionTradeSpreadBarDataViewModel optionTradeSpreadBarData);

        Task UpdateOptionTradeStateAsync(int orderId, int tradeId, TradeState tradeState, DateTime updatedOn, string updatedBy);
        Task UpdateTradePositionAsync(
            TradePositionId key, 
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
            int tradeId,
            TradeType tradeType,
            DateTime valueDate,
            int daysToExpiry,
            TradeStatus oldTradeStatus,
            TradeStatus newTradeStatus,
            DateTime updatedOn,
            string updatedBy);

        Task UpdateOptionLegDataAsync(OptionLegDataReadModel optionLegData);
        Task UpdateTradeLimitDailyProfitTarget(int tradeId, TradeType tradeType, decimal dailyProfitTarget, DateTime updatedOn, string updatedBy);
        Task UpdateTradeLiveFeedAsync(TradeLiveFeedReadModel tradeLiveFeed);
        Task UpdateTradeOrderStateAsync(TradeOrderId tradeTicketId, TradeOrderState tradeOrderState, DateTime updatedOn, string updatedBy);
        Task UpdateTradeOrderOrderPriceAsync(TradeOrderId tradeOrderId, decimal orderPrice, DateTime updatedOn, string updatedBy);

    }
}
