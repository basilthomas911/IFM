using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

/// <summary>
/// Defines Trade queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorTradeQueryApi : ITradeQueryApi
{
    Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId, CancellationToken cancellationToken);
    Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId, CancellationToken cancellationToken);
    Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId, CancellationToken cancellationToken);
    Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType, CancellationToken cancellationToken);
    Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId, CancellationToken cancellationToken);
    Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(int orderId, int tradeId, CancellationToken cancellationToken);
    Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate,
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken);
    Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(int orderId, CancellationToken cancellationToken);
    Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId, CancellationToken cancellationToken);
    Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(
        int orderId, int tradeId, TradeType tradeType, DateOnly valueDate,
        int daysToExpiry, TradeStatus tradeStatus, CancellationToken cancellationToken);
    Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(
        int tradeId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(
        int orderId, int tradeId, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(
        int orderId, int tradeId, DateOnly valueDate, int daysToExpiry,
        TradeStatus tradeStatus, CancellationToken cancellationToken);
    Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(
        int orderId, int tradeId, DateOnly valueDate, CancellationToken cancellationToken);
}
