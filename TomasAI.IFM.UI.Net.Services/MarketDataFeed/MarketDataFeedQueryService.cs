using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.UI.Net.Services.MarketDataFeed;

/// <summary>Provides the MarketDataFeedQueryService UI service boundary.</summary>
public class MarketDataFeedQueryService(IMarketDataFeedQueryApi marketDataFeedQueryApi) : UiServiceBase<MarketDataFeedQueryService>
{
    readonly IMarketDataFeedQueryApi _marketDataFeedQueryApi = IsArgumentNull.Set(marketDataFeedQueryApi);

    /// <summary>Reads the last saved option observation for one contract and value date.</summary>
    /// <param name="contractId">The option leg contract.</param>
    /// <param name="valueDate">The selected position's session date.</param>
    /// <returns>The saved observation, or null when none exists.</returns>
    public async Task<FuturesOptionTickDataV2ReadModel?> GetSavedOptionLegAsync(string contractId, DateOnly valueDate)
    {
        var result = await _marketDataFeedQueryApi.GetLastFuturesOptionTickDataAsync(contractId, valueDate).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"Saved option data query failed ({result.ErrorCode}): {result.ErrorMessage}");
        return result.Value;
    }

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
        => await ExecuteAsync(
            () => _marketDataFeedQueryApi.GetFuturesBarDataAsync(
                contractId,
                symbol,
                valueDate,
                EasternTime.ToUtc(startDate),
                EasternTime.ToUtc(endDate)),
            onCompleted);

    /// <summary>Reads a timestamp window across session partitions, including the supplied operational date.</summary>
    /// <param name="contractId">The displayed futures contract.</param>
    /// <param name="symbol">The displayed futures symbol.</param>
    /// <param name="valueDate">The operational value date, which may be held until EOD completes.</param>
    /// <param name="startDate">The inclusive timestamp window start.</param>
    /// <param name="endDate">The inclusive timestamp window end.</param>
    /// <param name="onCompleted">Receives the ordered bars after every partition read succeeds.</param>
    /// <returns>The asynchronous partition reads.</returns>
    public async Task GetFuturesBarWindowAsync(string contractId, string symbol, DateOnly valueDate,
        DateTime startDate, DateTime endDate, Action<FuturesBarDataReadModel[]> onCompleted)
    {
        var startUtc = EasternTime.ToUtc(startDate);
        var endUtc = EasternTime.ToUtc(endDate);
        if (endUtc < startUtc) throw new ArgumentOutOfRangeException(nameof(endDate));
        var zone = TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.MarketTimeZone;
        var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startUtc, zone));
        var lastDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(endUtc, zone));
        var valueDates = new SortedSet<DateOnly> { valueDate };
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            valueDates.Add(date);
            var opensUtc = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(18, 0)), zone);
            if (opensUtc <= endUtc && date.DayOfWeek is >= DayOfWeek.Sunday and <= DayOfWeek.Thursday)
                valueDates.Add(date.AddDays(1));
        }

        var bars = new List<FuturesBarDataReadModel>();
        foreach (var partitionDate in valueDates)
        {
            var completed = false;
            await GetFuturesBarDataAsync(contractId, symbol, partitionDate, startUtc, endUtc, values =>
            {
                bars.AddRange(values);
                completed = true;
            });
            if (!completed) return;
        }
        onCompleted([.. bars.Where(bar => bar.BarDate >= startUtc && bar.BarDate <= endUtc)
            .DistinctBy(bar => (bar.ContractId, bar.Symbol, bar.BarDate, bar.BarRateType))
            .OrderBy(bar => bar.BarDate)]);
    }

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

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task GetFuturesOptionSpreadDataAsync(
        DateOnly valueDate,
        DateOnly maturityDate,
        double assetPrice,
        double riskFreeRate,
        double timeValue,
        FuturesOptionContractReadModel shortOptionContract,
        FuturesOptionContractReadModel longOptionContract,
        Func<FuturesOptionSpreadDataReadModel, Task> onCompleted)
        => ExecuteAsync(
            () => _marketDataFeedQueryApi.GetFuturesOptionSpreadDataAsync(
                valueDate,
                maturityDate,
                assetPrice,
                riskFreeRate,
                timeValue,
                shortOptionContract,
                longOptionContract),
            onCompleted);

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

    /// <summary>Gets the authoritative backend feed state used for terminal-event reconciliation.</summary>
    public async Task<MarketDataFeedRuntimeStatusReadModel?> GetRuntimeStatusAsync()
    {
        var result = await _marketDataFeedQueryApi.GetRuntimeStatusAsync();
        return result.Success ? result.Value : null;
    }

    public async Task<DatabentoReadinessReadModel?> GetDatabentoReadinessAsync()
    {
        var result = await _marketDataFeedQueryApi.GetDatabentoReadinessAsync();
        return result.Success ? result.Value : null;
    }

    public async Task<DatabentoWatchdogObservationReadModel[]> GetDatabentoWatchdogHistoryAsync(int pageSize = 100)
    {
        var result = await _marketDataFeedQueryApi.GetDatabentoWatchdogHistoryAsync(pageSize: pageSize);
        return result.Success ? result.Value ?? [] : [];
    }

}
