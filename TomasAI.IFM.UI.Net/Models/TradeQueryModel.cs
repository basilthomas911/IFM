using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// create spread trade controller
/// </summary>
public class TradeQueryModel(ITradeQueryApi queryApi) : BaseModel<TradeQueryModel>
{
    readonly ITradeQueryApi _queryApi = queryApi ?? throw new ArgumentNullException(nameof(queryApi));

    /// <summary>
    /// load option trade
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    public async Task GetOptionTradeAsync(int orderId, int tradeId, Action<OptionTradeReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetOptionTradeAsync(orderId, tradeId), onCompleted);

    /// <summary>
    /// load option trade spread data
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetOptionTradeSpreadDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, Action<OptionTradeSpreadsDataModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetOptionTradeSpreadDataAsync(orderId, tradeId, tradeType, valueDate), onCompleted);

    /// <summary>
    /// load option trade spread bar data
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetOptionTradeSpreadBarDataAsync(int orderId, int tradeId, TradeType tradeType, DateOnly valueDate, DateTime startDate, DateTime endDate, Action<OptionTradeSpreadBarsDataModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetOptionTradeSpreadBarDataAsync(orderId, tradeId, tradeType, valueDate, startDate, endDate), onCompleted);

    /// <summary>
    /// load all spread trade data for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="onCompleted"></param>
    public async Task GetTradePositionsAsync(int orderId, int tradeId, Action<TradePositionReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetTradePositionsAsync(orderId, tradeId), onCompleted);

    /// <summary>
    /// load trade position trade types
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="onCompleted"></param>
    public async Task GetTradePositionTradeTypesAsync(int orderId, int tradeId, DateOnly valueDate, int daysToExpiry, TradeStatus tradeStatus, Action<string[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetTradePositionTradeTypesAsync(orderId, tradeId, valueDate, daysToExpiry, tradeStatus), onCompleted);

    /// <summary>
    /// load trade plan summary for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="orderId"></param>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetTradePlanActionAsync( int orderId, int tradeId, DateOnly valueDate, Action<TradePlanActionReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetTradePlanSummaryAsync(orderId, tradeId, valueDate), onCompleted);

    /// <summary>
    /// load single iron condor trade data for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="onDataLoad"></param>
    public async Task GetIronCondorTradePositionsAsync(
        int orderId,
        int tradeId, 
        DateOnly valueDate, 
        int daysToExpiry, 
        TradeType putSpreadTradeType,
        TradeType callSpreadTradeType,
       TradeStatus tradeStatus,
        Action<(TradePositionReadModel? PutCreditSpread, TradePositionReadModel? CallCreditSpread)> onViewAction)
    {
        var pcsServiceResult = await _queryApi.GetTradePositionAsync(orderId, tradeId, putSpreadTradeType, valueDate, daysToExpiry, tradeStatus);
        var ccsServiceResult = await _queryApi.GetTradePositionAsync(orderId, tradeId, callSpreadTradeType, valueDate, daysToExpiry, tradeStatus);
        onViewAction((PutCreditSpread: pcsServiceResult.Success ? pcsServiceResult.Value : default,
            CallCreditSpread: ccsServiceResult.Success ? ccsServiceResult.Value : default));
    }

    /// <summary>
    /// load single iron condor trade data for selected trade
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="onDataLoad"></param>
    public async Task<decimal> GetIronCondorNetSpreadAsync(
        int orderId,
        int tradeId,
        TradeType putSpreadTradeType,
        TradeType callSpreadTradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus)
     {
        var pcsServiceResult = await _queryApi.GetTradePositionAsync(orderId, tradeId, putSpreadTradeType, valueDate, daysToExpiry, tradeStatus);
        var ccsServiceResult = await _queryApi.GetTradePositionAsync(orderId, tradeId, callSpreadTradeType, valueDate, daysToExpiry, tradeStatus);
        if (pcsServiceResult.Success && ccsServiceResult.Success)
        {
            var pcsNetSpread = pcsServiceResult.Value!.NetSpread;
            var ccsNetSpread = ccsServiceResult.Value!.NetSpread;
            return pcsNetSpread + ccsNetSpread;
        }
        return default;
    }

    /// <summary>
    /// load trade history for selected trade order
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="onCompleted"></param>
    public async Task GetTradeHistoryAsync(int orderId, Action<TradeHistoryReadModel[]> onCompleted)
       => await ExecuteAsync(() => _queryApi.GetTradeHistoryAsync(orderId), onCompleted);

    /// <summary>
    /// get trade history for selected trade order
    /// </summary>
    /// <param name="orderId"></param>
    public async Task< List<TradeHistoryReadModel>> GetTradeHistoryAsync(int orderId)
    {
        var tradeHistory = new List<TradeHistoryReadModel>();
        var serviceResult = await _queryApi.GetTradeHistoryAsync(orderId);
        if (serviceResult.Success)
            tradeHistory.AddRange(serviceResult.Value!);
        return tradeHistory;
    }

    /// <summary>
    /// load trade info for all fund order trades
    /// </summary>
    /// <param name="fundOrderTrades"></param>
    /// <returns></returns>
    public async Task GetTradeInfoAsync(ICollection<FundOrderTradeReadModel> fundOrderTrades, Action<ICollection<TradeInfoReadModel>> onTradeInfoLoaded) 
    {
        var tradeInfo = new List<TradeInfoReadModel>();
        foreach (var e in fundOrderTrades)
        {
            var queryResult1 = await _queryApi.GetOptionLegContractIdsAsync(e.TradeId);
            if (!queryResult1.Success)
            {
                RaiseError(queryResult1.ErrorCode, queryResult1.ErrorMessage);
             }
            var optionLegContractIds = queryResult1.Value;
            var queryResult2 = await _queryApi.GetTradeQuantityAsync(e.TradeId);
            if (!queryResult2.Success)
            {
                RaiseError(queryResult2.ErrorCode, queryResult2.ErrorMessage);
             }

            var tradeQuantity = queryResult2.Value!.Value;
            tradeInfo.Add(new TradeInfoReadModel
            {
                TradeId = e.TradeId,
                OrderId = e.OrderId,
                TradeType = e.TradeType,
                TradeDate = e.TradeDate,
                Quantity = tradeQuantity,
                MaturityDate = e.MaturityDate,
                TradeState = e.TradeState,
                TradeAction = e.TradeAction,
                OptionLegContractIds = optionLegContractIds!
            });
        }
        onTradeInfoLoaded?.Invoke(tradeInfo);
    }

    /// <summary>
    /// load trade limits for selected trade 
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="onCompleted"></param>
    public async Task GetTradeLimitsAsync(int tradeId, Action<TradeLimitReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetTradeLimitAsync(tradeId), onCompleted);

    /// <summary>
    /// load trade limits for selected trade 
    /// </summary>
    /// <param name="tradeId"></param>
    /// <param name="tradeType"></param>
    /// <param name="onCompleted"></param>
    public async Task GetTradeTypeLimitAsync(int tradeId, TradeType tradeType, Action<TradeTypeLimitReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetTradeTypeLimitAsync(tradeId, tradeType), onCompleted);

    /// <summary>
    /// load iron condor market data indicator limit info 
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetIronCondorMDILimitAsync(int orderId, int tradeId, DateOnly valueDate, Action<IronCondorMDILimitDataModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetIronCondorMDILimitAsync(orderId, tradeId, valueDate), onCompleted);

}
