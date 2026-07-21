using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// market data analytics query model constructor
/// </summary>
/// <param name="queryApi"></param>
public class MarketDataAnalyticsQueryModel(IMarketDataAnalyticsQueryApi queryApi) : BaseModel<MarketDataAnalyticsQueryModel>
{
    readonly IMarketDataAnalyticsQueryApi _queryApi = queryApi;

    /// <summary>
    /// load futures trade signal
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate, Action<FuturesTradeSignalV2ReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesTradeSignalAsync(contractId, valueDate), onCompleted);

    /// <summary>
    /// get last futures trade signal
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetLastFuturesTradeSignalAsync(Action<FuturesTradeSignalV2ReadModel> onCompleted)
        => await ExecuteAsync(_queryApi.GetLastFuturesTradeSignalAsync, onCompleted);

    /// <summary>
    /// load futures iti trend direction changed signal
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, TradeTimePeriodType timePeriod, Action<FuturesItiSignalV2ReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesItiTrendDirectionChangedSignalsAsync(contractId, valueDate, timePeriod), onCompleted);

}
