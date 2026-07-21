using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.UI.Net.Models;

public class MarketDataFeedQueryModel(IMarketDataFeedQueryApi marketDataFeedQueryApi) : BaseModel<MarketDataFeedQueryModel>
{
    readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi = IsArgumentNull.Set(marketDataFeedQueryApi);

    /// <summary>
    /// return last futures eod data
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate, Action<FuturesEodDataV2ReadModel> onCompleted)
        => await ExecuteAsync(() => _marketDataFeedQueryApi.GetLastFuturesEodDataAsync(contractId, valueDate), onCompleted);

    /// <summary>
    /// Asynchronously retrieves the most recent futures bar data for the specified contract and symbol on the given
    /// date.
    /// </summary>
    /// <remarks>Ensure that both contractId and symbol are valid and correspond to an active futures contract
    /// to avoid errors during data retrieval. This method is intended for scenarios requiring up-to-date market data
    /// for futures contracts.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract for which bar data is requested. Cannot be null or empty.</param>
    /// <param name="symbol">The symbol representing the futures contract. Must be a valid symbol recognized by the data provider.</param>
    /// <param name="valueDate">The date for which the futures bar data is to be retrieved. Specifies the trading day of interest.</param>
    /// <param name="onCompleted">An action to invoke when the data retrieval is complete. Receives an array of FuturesBarDataReadModel objects
    /// containing the bar data.</param>
    /// <returns>A task that represents the asynchronous operation. The task completes when the bar data has been retrieved and
    /// the completion action has been invoked.</returns>
    public async Task GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, Action<FuturesBarDataReadModel> onCompleted)
        => await ExecuteAsync(() => _marketDataFeedQueryApi.GetLastFuturesBarDataAsync(contractId, symbol, valueDate), onCompleted);

    /// <summary>
    /// return futures eod data by date range
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesEodDataAsync(string contractId, DateOnly startDate, DateOnly endDate, Action<FuturesEodDataV2ReadModel[]> onCompleted)
        => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, startDate, endDate), onCompleted);

    /// <summary>
    /// return futures risk position type
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesRiskPositionTypeAsync(DateOnly valueDate, TradeType tradeType, Action<RiskPositionTypeReadModel> onCompleted)
        => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesRiskPositionTypeAsync(valueDate, tradeType), onCompleted);

    /// <summary>
    /// return futures eod data by value date
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesEodDataAsync(string contractId, DateOnly valueDate, Action<FuturesEodDataV2ReadModel> onCompleted)
        => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesEodDataAsync(contractId, valueDate), onCompleted);

    /// <summary>
    /// return futures bar data by date range
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate, Action<FuturesBarDataReadModel[]> onCompleted)
        => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesBarDataAsync(contractId, symbol, valueDate, startDate, endDate), onCompleted);

    /// <summary>
    /// return futures option spread data
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="maturityDate"></param>
    /// <param name="assetPrice"></param>
    /// <param name="riskFreeRate"></param>
    /// <param name="timeValue"></param>
    /// <param name="shortOptionContract"></param>
    /// <param name="longOptionContract"></param>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesOptionSpreadDataAsync(
        DateOnly valueDate,
        DateOnly maturityDate,
        double assetPrice,
        double riskFreeRate,
        double timeValue,
        FuturesOptionContractReadModel shortOptionContract,
        FuturesOptionContractReadModel longOptionContract,
        Action<FuturesOptionSpreadDataReadModel> onCompleted)
            => await ExecuteAsync(() => _marketDataFeedQueryApi.GetFuturesOptionSpreadDataAsync(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, shortOptionContract, longOptionContract), onCompleted);

    /// <summary>
    /// return streaming request id by stream id
    /// </summary>
    /// <returns></returns>
    public async Task<int> GetStreamingRequestIdAsync()
    {
        var serviceResult = await _marketDataFeedQueryApi.GetStreamingRequestIdAsync();
        return serviceResult.Success && serviceResult.Value is not null
            ? serviceResult.Value.AsInteger
            : -1;
    }

    /// <summary>
    /// return option quote id 
    /// </summary>
    /// <returns></returns>
    public async Task<int> GetOptionQuoteIdAsync()
    {
        var serviceResult = await _marketDataFeedQueryApi.GetOptionQuoteIdAsync();
        return serviceResult.Success && serviceResult.Value is not null
            ? serviceResult.Value.AsInteger
            : -1;
    }
}
