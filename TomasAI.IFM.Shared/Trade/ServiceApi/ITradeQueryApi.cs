using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.ServiceApi;

public interface ITradeQueryApi
{
    Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId);
    Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId);
    Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId);
    Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType);
    Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId);
    Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(int orderId, int tradeId);
    Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate);
    Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, DateTime startDate, DateTime endDate);
    Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(int orderId);
    Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId);
    Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus);
    Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate);
    Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync( int orderId, int tradeId, DateOnly valueDate);
    Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(
        int orderId,
        int tradeId,
         DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus);
    Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(int orderId, int tradeId, DateOnly valueDate);
}
