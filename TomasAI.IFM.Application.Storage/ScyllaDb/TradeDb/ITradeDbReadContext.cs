using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

public interface ITradeDbReadContext 
{
    Task<OptionTradeReadModel?> GetOptionTradeAsync(int orderId, int tradeId);
    Task<OptionTradeSpreadsDataModel?> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    Task<ICollection<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync();
    Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType, DateTime startDate, DateTime endDate);
    Task<ICollection<OptionTradeSpreadBarsDataModel>> GetOptionTradeSpreadBarDataAsync();
    Task<TradePriceReadModel?> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate);
    Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync(int orderId);
    Task<ICollection<OptionTradeReadModel>> GetOptionTradesAsync();
    Task<ICollection<OptionTradeLegReadModel>> GetOptionLegsAsync();
    Task<ICollection<OptionTradeLegDataReadModel>> GetOptionLegDataAsync();
    Task<TradePositionReadModel?> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus);
    Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync(int orderId, int tradeId);
    Task<ICollection<TradePositionReadModel>> GetTradePositionsAsync();
    Task<ICollection<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId);
    Task<ICollection<string>> GetOptionLegContractIdsAsync(int tradeId);
    Task<int> GetTradeQuantityAsync(int tradeId);
    Task<TradeLimitReadModel?> GetTradeLimitAsync(int tradeId);
    Task<ICollection<TradeLimitReadModel>> GetTradeLimitsAsync();

    Task<TradePlanStopLossLimitReadModel?> GetTradePlanStopLossLimitAsync(int orderId, int tradeId);
    Task<TradeTypeLimitReadModel?> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType);
    Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync(int tradeId);
    Task<ICollection<TradeTypeLimitReadModel>> GetTradeTypeLimitsAsync();
    Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync(int orderId, int tradeId);
    Task<ICollection<TradeFillReadModel>> GetTradeFillsAsync();
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync();
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId);
    Task<ICollection<TradePlanReadModel>> GetLastTradePlansAsync(int orderId, int tradeId);
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly valueDate);
    Task<ICollection<TradePlanReadModel>> GetTradePlansAsync(int orderId, int tradeId, DateOnly startDate, DateOnly endDate);
    Task<ICollection<TradePlanForwardLossRatioReadModel>> GetTradePlanForwardLossRatiosAsync(DateOnly startDate, DateOnly endDate);
    Task<TradePlanForwardLossRatioReadModel?> GetTradePlanForwardLossRatioAsync(DateOnly valueDate);
    Task<TradeOrderReadModel?> GetTradeOrderAsync(DateOnly valueDate, int tradeId);
    Task<ICollection<TradeOrderReadModel>> GetTradeOrdersAsync(DateOnly startDate, DateOnly endDate);
    Task<ICollection<TradeOrderReadModel>> GetTradeOrdersByFundIdAsync(DateOnly valueDate, int fundId);
    Task<ICollection<TradeFillDataReadModel>> GetTradeFillDataAsync(int tradeId);
    Task<ICollection<TradeLiveFeedReadModel>> GetTradeLiveFeedAsync(int orderId, int tradeId);
    Task<ICollection<string>> GetTradePositionTradeTypesAsync(
       int orderId,
       int tradeId,
       DateOnly valueDate,
        TradeStatus tradeStatus,
       int daysToExpiry);
    Task<TradePlanForwardLossLimitReadModel?> GetTradePlanForwardLossLimitAsync(int orderId, int tradeId, DateOnly valueDate, TradeType tradeType);
    Task<TradePlacementSignalReadModel?> GetTradePlacementSignalAsync(string contractId, DateOnly valueDate);
}
