using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeDb
{
    public interface ITradeDbReadContext : ITradeDbContext
    {
        Task<OptionTradeDataModel> GetOptionTradeAsync(int orderId, int tradeId);
        Task<OptionTradeSpreadsDataModel> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate);
        Task<IReadOnlyList<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate, DateTime startDate, DateTime endDate);
        Task<TradePriceReadModel> GetIronCondorTradePriceAsync(int tradeId, DateTime valueDate);
        Task<IReadOnlyList<OptionTradeDataModel>> GetOptionTradesAsync(int orderId);
        Task<TradePositionReadModel> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateTime valueDate, int daysToExpiry, TradeStatus tradeStatus);
        Task<IReadOnlyList<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId);
        Task<IReadOnlyList<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId);
        Task<IReadOnlyList<string>> GetOptionLegContractIdsAsync(int tradeId);
        Task<int> GetTradeQuantityAsync(int tradeId);
        Task<TradeLimitReadModel> GetTradeLimitAsync(int tradeId);
        Task<TradePlanStopLossLimitReadModel> GetTradePlanStopLossLimitAsync(int orderId, int tradeId);
        Task<TradeTypeLimitReadModel> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType);
        Task<IReadOnlyList<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync(int tradeId);
        Task<IReadOnlyList<TradeFillReadModel>> GetTradeFillsAsync(int tradeId);
        Task<IReadOnlyList<TradePlanReadModel>> GetTradePlansAsync(int orderId);
        Task<IReadOnlyList<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateTime valueDate);
        Task<IReadOnlyList<TradePlanReadModel>> GetTradePlansAsync(DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<TradePlanActionReadModel>> GetTradePlanActionAsync( int orderId, int tradeId, DateTime valueDate);
        Task<IReadOnlyList<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateTime startDate, DateTime endDate);
        Task<TradePlanForwardLossRatioReadModel> GetTradePlanForwardLossRatioAsync(DateTime valueDate);
        Task<IReadOnlyList<TradeOrderReadModel>> GetTradeOrdersAsync(DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<TradeFillDataReadModel>> GetTradeFillDataAsync(int fundId, int orderId, int tradeId);
        Task<IReadOnlyList<TradeLiveFeedReadModel>> GetTradeLiveFeedAsync(int orderId, int tradeId);
        Task<IReadOnlyList<TradeDiaryEntryReadModel>> GetTradeDiaryAsync(int orderId, int tradeId);
        Task<TradePositionState> GetTradePositionStateAsync(OptionTradeEntityId id);
        Task<IReadOnlyList<string>> GetTradePositionTradeTypesAsync(
           int orderId,
           int tradeId,
           DateTime valueDate,
           int daysToExpiry,
           TradeStatus tradeStatus);
        Task<TradePlanForwardLossLimitReadModel> GetTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId id);
    }
}
