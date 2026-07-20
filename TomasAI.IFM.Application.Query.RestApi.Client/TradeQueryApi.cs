using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.Client;

public class TradeQueryApi(IQueryService querySvc) : ITradeQueryApi
{
    readonly IQueryService _querySvc = IsArgumentNull.Set(querySvc);
    readonly string _controller = "Trade";

    /// <summary>
    /// return trade history for selected trade order
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradeHistoryQuery(orderId), _controller);

    /// <summary>
    /// return option lep contract ids
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId)
        => await _querySvc.ExecuteApiQueryAsync(new GetOptionLegContractIdsQuery( tradeId), _controller);

    /// <summary>
    /// return trade limit for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradeLimitQuery(tradeId), _controller);

    /// <summary>
    /// return trade type limit
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType )
        => await _querySvc.ExecuteApiQueryAsync(new GetTradeTypeLimitQuery( tradeId , tradeType), _controller);

    /// <summary>
    /// return trade quantity
    /// </summary>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradeQuantityQuery (tradeId), _controller);

    /// <summary>
    /// return option trade
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<OptionTradeDataModel>> GetOptionTradeAsync(int orderId, int tradeId)
        => await _querySvc.ExecuteApiQueryAsync(new GetOptionTradeQuery(orderId,  tradeId), _controller);

    /// <summary>
    /// return option trades
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<OptionTradeDataModel[]>> GetOptionTradesAsync(int orderId)
        => await _querySvc.ExecuteApiQueryAsync(new GetOptionTradesQuery(orderId), _controller);

    /// <summary>
    /// return option trade spread data
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetOptionTradeSpreadDataQuery(orderId, tradeId, tradeType, valueDate), _controller);

    /// <summary>
    /// return option trade spread bar data
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, DateTime startDate, DateTime endDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetOptionTradeSpreadBarDataQuery(orderId, tradeId, tradeType, valueDate, startDate, endDate), _controller);

    /// <summary>
    /// return trade positions
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePositionsQuery(orderId, tradeId), _controller);

    /// <summary>
    /// return trade positions by trade type
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="daysToExpiry"></param>
    /// <param name="tradeStatus"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePositionQuery(orderId, tradeId , tradeType, valueDate, daysToExpiry, tradeStatus), _controller);

    /// <summary>
    /// return iron condor trade price
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorTradePriceQuery(tradeId, valueDate), _controller);

    /// <summary>
    /// return trade plan actions
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(DateOnly valueDate, int orderId, int tradeId)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePlanActionQuery ( orderId, tradeId, valueDate), _controller);

    /// <summary>
    /// return trade position trade types
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(
        int orderId,
        int tradeId,
         DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradePositionTradeTypesQuery(orderId,  tradeId, valueDate, daysToExpiry, tradeStatus), _controller);

    /// <summary>
    /// eturn iron condor market data indicator limit info
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(int orderId, int tradeId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorMDILimitQuery(orderId, tradeId, valueDate), _controller);
}
