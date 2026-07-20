using System.Threading.Tasks;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.QueryParameters;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Application;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// Client implementation for trade-related queries using an <see cref="IQueryServiceApi"/>.
/// </summary>
public class OptionTradeQueryApi(IQueryServiceApi querySvc) : ITradeQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// Return trade history for selected trade order
    /// </summary>
    public async Task<ServiceResult<TradeHistoryReadModel[]>> GetTradeHistoryAsync(int orderId)
    {
        var qryParam = new GetTradeHistoryParameter(orderId);
        return await _querySvc.ExecuteQueryAsync<TradeHistoryReadModel[]>(TradeQueryUriPath.GetTradeHistory, qryParam, GetTradeHistoryQuery.ErrorId);
    }

    /// <summary>
    /// Return option leg contract ids
    /// </summary>
    public async Task<ServiceResult<string[]>> GetOptionLegContractIdsAsync(int tradeId)
    {
        var qryParam = new GetOptionLegContractIdsParameter(tradeId);
        return await _querySvc.ExecuteQueryAsync<string[]>(TradeQueryUriPath.GetOptionLegContractIds, qryParam, GetOptionLegContractIdsQuery.ErrorId);
    }

    /// <summary>
    /// Return trade limit for selected trade
    /// </summary>
    public async Task<ServiceResult<TradeLimitReadModel>> GetTradeLimitAsync(int tradeId)
    {
        var qryParam = new GetTradeLimitParameter(tradeId);
        return await _querySvc.ExecuteQueryAsync<TradeLimitReadModel>(TradeQueryUriPath.GetTradeLimit, qryParam, GetTradeLimitQuery.ErrorId);
    }

    /// <summary>
    /// Return trade type limit
    /// </summary>
    public async Task<ServiceResult<TradeTypeLimitReadModel>> GetTradeTypeLimitAsync(int tradeId, TradeType tradeType)
    {
        var qryParam = new GetTradeTypeLimitParameter(tradeId, tradeType);
        return await _querySvc.ExecuteQueryAsync<TradeTypeLimitReadModel>(TradeQueryUriPath.GetTradeTypeLimit, qryParam, GetTradeTypeLimitQuery.ErrorId);
    }

    /// <summary>
    /// Return trade quantity
    /// </summary>
    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradeQuantityAsync(int tradeId)
    {
        var qryParam = new GetTradeQuantityParameter(tradeId);
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(TradeQueryUriPath.GetTradeQuantity, qryParam, GetTradeQuantityQuery.ErrorId);
    }

    /// <summary>
    /// Return option trade
    /// </summary>
    public async Task<ServiceResult<OptionTradeReadModel>> GetOptionTradeAsync(int orderId, int tradeId)
    {
        var qryParam = new GetOptionTradeParameter(orderId, tradeId);
        return await _querySvc.ExecuteQueryAsync<OptionTradeReadModel>(TradeQueryUriPath.GetOptionTrade, qryParam, GetOptionTradeQuery.ErrorId);
    }

    /// <summary>
    /// Return option trade spread data
    /// </summary>
    public async Task<ServiceResult<OptionTradeSpreadsDataModel>> GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate)
    {
        var qryParam = new GetOptionTradeSpreadDataParameter(orderId, tradeId, tradeType, valueDate);
        return await _querySvc.PostQueryAsync<OptionTradeSpreadsDataModel>(TradeQueryUriPath.GetOptionTradeSpreadData, qryParam, GetOptionTradeSpreadDataQuery.ErrorId);
    }

    /// <summary>
    /// Return option trade spread bar data
    /// </summary>
    public async Task<ServiceResult<OptionTradeSpreadBarsDataModel[]>> GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, System.DateTime startDate, System.DateTime endDate)
    {
        var qryParam = new GetOptionTradeSpreadBarDataParameter(orderId, tradeId, tradeType, valueDate, startDate, endDate);
        return await _querySvc.PostQueryAsync<OptionTradeSpreadBarsDataModel[]>(TradeQueryUriPath.GetOptionTradeSpreadBarData, qryParam, GetOptionTradeSpreadBarDataQuery.ErrorId);
    }

    /// <summary>
    /// Return option trades
    /// </summary>
    public async Task<ServiceResult<OptionTradeReadModel[]>> GetOptionTradesAsync(int orderId)
    {
        var qryParam = new GetOptionTradesParameter(orderId);
        return await _querySvc.ExecuteQueryAsync<OptionTradeReadModel[]>(TradeQueryUriPath.GetOptionTrades, qryParam, GetOptionTradesQuery.ErrorId);
    }

    /// <summary>
    /// Return trade positions
    /// </summary>
    public async Task<ServiceResult<TradePositionReadModel[]>> GetTradePositionsAsync(int orderId, int tradeId)
    {
        var qryParam = new GetTradePositionsParameter(orderId, tradeId);
        return await _querySvc.ExecuteQueryAsync<TradePositionReadModel[]>(TradeQueryUriPath.GetTradePositions, qryParam, GetTradePositionsQuery.ErrorId);
    }

    /// <summary>
    /// Return trade position by params
    /// </summary>
    public async Task<ServiceResult<TradePositionReadModel>> GetTradePositionAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        var qryParam = new GetTradePositionParameter(orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus);
        return await _querySvc.ExecuteQueryAsync<TradePositionReadModel>(TradeQueryUriPath.GetTradePosition, qryParam, GetTradePositionQuery.ErrorId);
    }

    /// <summary>
    /// Return iron condor trade price
    /// </summary>
    public async Task<ServiceResult<TradePriceReadModel>> GetIronCondorTradePriceAsync(int tradeId, DateOnly valueDate)
    {
        var qryParam = new GetIronCondorTradePriceParameter(tradeId, valueDate);
        return await _querySvc.ExecuteQueryAsync<TradePriceReadModel>(TradeQueryUriPath.GetIronCondorTradePrice, qryParam, GetIronCondorTradePriceQuery.ErrorId);
    }

    /// <summary>
    /// Return trade plan summary
    /// </summary>
    public async Task<ServiceResult<TradePlanActionReadModel[]>> GetTradePlanSummaryAsync(int orderId, int tradeId,DateOnly valueDate)
    {
        var qryParam = new GetTradePlanSummaryParameter( orderId, tradeId, valueDate);
        return await _querySvc.ExecuteQueryAsync<TradePlanActionReadModel[]>(TradeQueryUriPath.GetTradePlanSummary, qryParam, GetTradePlanActionQuery.ErrorId);
    }

    /// <summary>
    /// Return trade position trade types
    /// </summary>
    public async Task<ServiceResult<string[]>> GetTradePositionTradeTypesAsync(int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus)
    {
        var qryParam = new GetTradePositionTradeTypesParameter(orderId, tradeId, valueDate, daysToExpiry, tradeStatus);
        return await _querySvc.ExecuteQueryAsync<string[]>(TradeQueryUriPath.GetTradePositionTradeTypes, qryParam, GetTradePositionTradeTypesQuery.ErrorId);
    }

    /// <summary>
    /// Return iron condor MDI limit
    /// </summary>
    public async Task<ServiceResult<IronCondorMDILimitDataModel>> GetIronCondorMDILimitAsync(int orderId, int tradeId, DateOnly valueDate)
    {
        var qryParam = new GetIronCondorMDILimitParameter(orderId, tradeId, valueDate);
        return await _querySvc.ExecuteQueryAsync<IronCondorMDILimitDataModel>(TradeQueryUriPath.GetIronCondorMDILimit, qryParam, GetIronCondorMDILimitQuery.ErrorId);
    }
}
