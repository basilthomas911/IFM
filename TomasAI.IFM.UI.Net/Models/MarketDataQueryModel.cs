using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// create market data query model
/// </summary>
/// <param name="appRoot"></param>
public class MarketDataQueryModel(IMarketDataQueryApi queryApi, IMarketDataFeedQueryApi queryFeedApi) 
    : BaseModel<MarketDataQueryModel>
{
    readonly IMarketDataQueryApi _queryApi = IsArgumentNull.Set(queryApi);
    readonly IMarketDataFeedQueryApi _queryFeedApi = IsArgumentNull.Set(queryFeedApi);

    /// <summary>
    /// load futures contract
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetFuturesContractAsync(string contractId, Action<FuturesContractV2ReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesContractAsync(contractId), onCompleted);

    /// <summary>
    /// load futures contract
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesContractsAsync(Action<FuturesContractV2ReadModel[]> onCompleted)
        => await ExecuteAsync(_queryApi.GetFuturesContractsAsync, onCompleted);

    /// <summary>
    /// load currently traded futures contract
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task GetCurrentlyTradedFuturesContractAsync(Action<FuturesContractV2ReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetCurrentlyTradedFuturesContractAsync("ES"), onCompleted);

    /// <summary>
    /// load currently traded futures contract
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task GetCurrentlyTradedFuturesContractsAsync(Action< ICollection<FuturesContractV2ReadModel>> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetCurrentlyTradedFuturesContractsAsync("ES"), onCompleted);
    
    /// <summary>
    /// load single futures option contract
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesOptionContractAsync(string contractId, Action<FuturesOptionContractReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesOptionContractAsync(contractId), onCompleted);
    
    /// <summary>
    ///return list of existing futures option contract ids
    /// </summary>
    /// <param name="contractIds"></param>
    public async Task GetFuturesOptionContractIdsAsync(string[] contractIds , Action<string[]> onCompleted) 
        => await ExecuteAsync(() => _queryApi.GetFuturesOptionContractIdsAsync(contractIds), onCompleted);

    /// <summary>
    /// load futures option contracts by symbol
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesOptionContractsAsync(string symbol, Action<FuturesOptionContractReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesOptionContractsAsync(symbol), onCompleted);

    /// <summary>
    /// get yield curve rate time periods
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetYieldCurveRateTimePeriodsAsync(Action<string[]> onCompleted) 
    {
        var serviceResult = await _queryApi.GetYieldCurveRateYearsAsync();
        if (serviceResult.Success)
        {
            List<string> timePeriods = [ "Current Month"];
            var modelYears = serviceResult.Value;
            if (modelYears != null)
                foreach (var e in modelYears.Years.Distinct())
                    timePeriods.Add($"{e}");
            onCompleted?.Invoke([.. timePeriods]);
        }
        else
            RaiseError(serviceResult.ErrorCode, serviceResult.ErrorMessage);
    }

    /// <summary>
    /// get value date
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetValueDateAsync(Action<DateOnly?> onCompleted)
        => await ExecuteAsync(_queryApi.GetValueDateAsync, vm => onCompleted(vm?.Value));

    /// <summary>
    /// get yield curve rates by date range
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate, Action<YieldCurveRateReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetYieldCurveRatesAsync(startDate, endDate), onCompleted);

    /// <summary>
    /// get external yield curve rates
    /// </summary>
    /// <returns></returns>
    public async Task GetExternalYieldCurveRatesAsync(Action<YieldCurveRateReadModel[]> onCompleted)
        => await ExecuteAsync( _queryApi.GetExternalYieldCurveRatesAsync, onCompleted);

    /// <summary>
    /// get risk free rate
    /// </summary>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetRiskFreeRateAsync(Action<double> onCompleted)
        => await ExecuteAsync( _queryApi.GetLastYieldCurveRateAsync, ycr => onCompleted(ycr.OneMonth / 100));

    /// <summary>
    /// get trading dates
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="marketType"></param>
    /// <param name="currencyType"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, Action<DateOnly[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetTradingDatesAsync(startDate, endDate, marketType, currencyType), onCompleted);

    /// <summary>
    /// get trading days by date range
    /// </summary>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="marketType"></param>
    /// <param name="currencyType"></param>
    /// <param name="onCompleted"></param>
    public async Task GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, Action<int> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetTradingDaysAsync(startDate, endDate, marketType, currencyType), vm => onCompleted(vm.Value));

    /// <summary>
    /// return current traded futures eod data
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetCurrentFuturesEodDataAsync(DateOnly valueDate, Action<FuturesEodDataV2ReadModel> onCompleted)
    {
        var serviceResult = await _queryApi.GetCurrentlyTradedFuturesContractAsync("ES");
        if (serviceResult.Success)
        {
            var contractId = serviceResult.Value!.ContractId;
            var serviceResult2 = await _queryFeedApi.GetFuturesEodDataAsync(contractId, valueDate);
            if (serviceResult2.Success)
                onCompleted?.Invoke(serviceResult2.Value!);
            else
            {
                serviceResult2 = await _queryFeedApi.GetLastFuturesEodDataAsync(contractId, valueDate);
                if (serviceResult2.Success)
                    onCompleted?.Invoke(serviceResult2.Value!);
                else
                    RaiseError(serviceResult2.ErrorCode, serviceResult2.ErrorMessage);
            }
        }
        else
            RaiseError(serviceResult.ErrorCode, serviceResult.ErrorMessage);
    }

    /// <summary>
    /// check if yield curve rate exists
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task YieldCurveRateExistsAsync(DateOnly  valueDate, Action<ServiceResult<ScalarReadModel<bool>>> onCompleted)
    {
        var serviceResult = await _queryApi.YieldCurveRateExistsAsync(valueDate);
        onCompleted?.Invoke(serviceResult);
    }
}
