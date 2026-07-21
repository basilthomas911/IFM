using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Shared.TradePlan.ViewModels;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// create strade plan query model
/// </summary>
public class TradePlanQueryModel(ITradePlanQueryApi queryApi) 
    : BaseModel<TradePlanQueryModel>
{
    readonly ITradePlanQueryApi _queryApi = IsArgumentNull.Set( queryApi);

    /// <summary>
    /// load iron condor forward delta
    /// </summary>
    /// <param name="vixContractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <param name="riskPositionType"></param>
    /// <param name="onCompleted"></param>
    public async Task GetIronCondorForwardDeltaAsync(string vixContractId, DateOnly valueDate, TradeType tradeType, RiskPositionType riskPositionType, Action<IronCondorForwardDeltaDataModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetIronCondorForwardDeltaAsync(vixContractId, valueDate, tradeType, riskPositionType), onCompleted);

    /// <summary>
    /// load trade plans selected trade
    /// </summary>
    /// <param name="orderId"></param>
    /// <param name="tradeId"></param>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetTradePlansAsync( int orderId, int tradeId, DateOnly valueDate, Action<TradePlanReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetTradePlansAsync(orderId, tradeId, valueDate), onCompleted);
}
