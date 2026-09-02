using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;

namespace TomasAI.IFM.UI.Net.Services.Analytics;

/// <summary>
/// market data analytics query model constructor
/// </summary>
/// <param name="queryApi"></param>
public class MarketDataAnalyticsQueryService(IMarketDataAnalyticsQueryApi queryApi) : UiServiceBase<MarketDataAnalyticsQueryService>
{
    readonly IMarketDataAnalyticsQueryApi _queryApi = queryApi;

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public async Task GetMarketOutlookSnapshotAsync(
        string contractId,
        DateOnly valueDate,
        bool loadPersistedBaseline,
        Action<MarketOutlookReadModel> onCompleted)
        => await ExecuteAsync(
            () => _queryApi.GetMarketOutlookSnapshotAsync(
                contractId,
                valueDate,
                loadPersistedBaseline),
            onCompleted);

    /// <summary>Reads the current process-local Market Outlook without reloading storage.</summary>
    public Task GetMarketOutlookSnapshotAsync(
        string contractId,
        DateOnly valueDate,
        Action<MarketOutlookReadModel> onCompleted) =>
        GetMarketOutlookSnapshotAsync(
            contractId,
            valueDate,
            loadPersistedBaseline: false,
            onCompleted: onCompleted);

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
    public async Task GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateOnly valueDate, TimeFrameType timePeriod, Action<FuturesItiSignalV2ReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesItiTrendDirectionChangedSignalsAsync(contractId, valueDate, timePeriod), onCompleted);

}
