using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.UI.Net.Services.MarketData;

/// <summary>
/// create market data query model
/// </summary>
/// <param name="appRoot"></param>
public class MarketDataQueryService(IMarketDataQueryApi queryApi, IMarketDataFeedQueryApi queryFeedApi)
    : UiServiceBase<MarketDataQueryService>
{
    public Task<ServiceResult<EvaluatedOptionChainReadModel>> QueryEvaluatedOptionChainAsync(GetEvaluatedOptionChainQuery request,CancellationToken token=default)
        => _queryApi.GetEvaluatedOptionChainAsync(request,token);

    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> QueryFuturesEodDataAsync(
        string contractId, DateOnly valueDate, CancellationToken token = default)
    {
        var current = await _queryFeedApi.GetFuturesEodDataAsync(contractId, valueDate).WaitAsync(token);
        return current.Success && current.Value is { ClosePrice: > 0, DailyStdDevAmount: > 0 }
            && double.IsFinite(current.Value.DailyStdDevAmount)
            ? current
            : await _queryFeedApi.GetLastFuturesEodDataAsync(contractId, valueDate).WaitAsync(token);
    }
    static readonly string[] DashboardSymbols = ["ES", "VX"];
    readonly IMarketDataQueryApi _queryApi = IsArgumentNull.Set(queryApi);
    readonly IMarketDataFeedQueryApi _queryFeedApi = IsArgumentNull.Set(queryFeedApi);
    public Task<ServiceResult<InstrumentDefinitionPage>> GetInstrumentDefinitionsAsync(InstrumentDefinitionPageRequest request,
        CancellationToken cancellationToken = default) => _queryApi.GetInstrumentDefinitionsAsync(request, cancellationToken);
    public Task<ServiceResult<FuturesContractV3ReadModel>> ResolveUnderlyingAsync(string contractId,
        CancellationToken cancellationToken = default) => _queryApi.GetFuturesContractAsync(contractId).WaitAsync(cancellationToken);

    /// <summary>Loads provider-backed symbols and their currency/exchange by strategy family.</summary>
    public Task<ServiceResult<TradeStrategySymbolReadModel[]>> GetTradeStrategySymbolsAsync(
        TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType family, CancellationToken cancellationToken = default)
        => _queryApi.GetTradeStrategySymbolsAsync(family, cancellationToken);

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task LoadEconomicCalendarAsync(
        DateTime todaysDate,
        EconomicCalendarViewType calendarViewType,
        string countryCode,
        Action<EconomicCalendarReadModel[]> onCompleted)
        => ExecuteAsync(
            () => _queryApi.GetEconomicCalendarsAsync(
                EasternTime.ToUtc(todaysDate),
                calendarViewType,
                countryCode),
            onCompleted);

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task LoadEconomicCalendarAsync(
        DateTime todaysDate,
        EconomicCalendarViewType calendarViewType,
        string countryCode,
        Func<EconomicCalendarReadModel[], Task> onCompleted)
        => ExecuteAsync(
            () => _queryApi.GetEconomicCalendarsAsync(
                EasternTime.ToUtc(todaysDate),
                calendarViewType,
                countryCode),
            onCompleted);

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task LoadEconomicCalendarsAsync(
        DateOnly eventDate,
        string countryCode,
        Action<EconomicCalendarReadModel[]> onCompleted)
        => ExecuteAsync(
            () => _queryApi.GetEconomicCalendarsAsync(
                EasternTime.ToUtc(eventDate.ToDateTime(TimeOnly.MinValue)),
                EconomicCalendarViewType.Today,
                countryCode),
            onCompleted);

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task LoadEconomicCalendarCountryCodesAsync(
        Action<EconomicCalendarCountryCodeReadModel[]> onCompleted)
        => ExecuteAsync(_queryApi.GetEconomicCalendarCountryCodesAsync, onCompleted);

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task LoadEconomicCalendarDateAsync(
        DateTime todaysDate,
        EconomicCalendarViewType calendarViewType,
        Action<string> onCompleted)
        => ExecuteAsync(
            () => _queryApi.GetEconomicCalendarDateAsync(
                EasternTime.ToUtc(todaysDate),
                calendarViewType),
            onCompleted);


    /// <summary>
    /// load futures contract
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetFuturesContractAsync(string contractId, Action<FuturesContractV3ReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesContractAsync(contractId), onCompleted);

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task GetFuturesContractAsync(string contractId, Func<FuturesContractV3ReadModel, Task> onCompleted)
        => ExecuteAsync(() => _queryApi.GetFuturesContractAsync(contractId), onCompleted);

    /// <summary>
    /// load futures contract
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task GetFuturesContractsAsync(Action<FuturesContractV3ReadModel[]> onCompleted)
        => await ExecuteAsync(_queryApi.GetFuturesContractsAsync, onCompleted);

    /// <summary>
    /// load currently traded futures contract
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task GetOnTheRunFuturesContractAsync(Action<FuturesContractV3ReadModel> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetOnTheRunFuturesContractAsync("ES"), onCompleted);

    /// <summary>
    /// load currently traded futures contract
    /// </summary>
    /// <param name="onCompleted"></param>
    public async Task GetRolloverFuturesContractsAsync(Action<ICollection<FuturesContractV3ReadModel>> onCompleted)
    {
        ArgumentNullException.ThrowIfNull(onCompleted);
        List<FuturesContractV3ReadModel> contracts = [];
        foreach (var symbol in DashboardSymbols)
        {
            await ExecuteAsync(
                () => _queryApi.GetRolloverFuturesContractsAsync(symbol),
                values => contracts.AddRange(values));
        }
        onCompleted(contracts
            .DistinctBy(contract => contract.ContractId)
            .ToArray());
    }

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public async Task GetRolloverFuturesContractsAsync(Func<ICollection<FuturesContractV3ReadModel>, Task> onCompleted)
    {
        ArgumentNullException.ThrowIfNull(onCompleted);
        ICollection<FuturesContractV3ReadModel> contracts = [];
        await GetRolloverFuturesContractsAsync(values => contracts = values);
        await onCompleted(contracts);
    }

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
    public Task<ServiceResult<FuturesOptionContractPageReadModel>> GetFuturesOptionContractsPageAsync(
        TomasAI.IFM.Domain.MarketData.Shared.QueryParameters.GetFuturesOptionContractsPageParameter request,
        CancellationToken cancellationToken = default)
        => _queryApi.GetFuturesOptionContractsPageAsync(request, cancellationToken);

    public async Task GetFuturesOptionContractsAsync(string symbol, Action<FuturesOptionContractReadModel[]> onCompleted)
        => await ExecuteAsync(() => _queryApi.GetFuturesOptionContractsAsync(symbol), onCompleted);

    public Task GetDatabentoOptionChainRangeAsync(
        string underlyingSymbol,
        DateOnly fromMaturityDate,
        DateOnly throughMaturityDate,
        Action<OptionContractExpiryReadModel[]> onCompleted,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            () => _queryApi.GetDatabentoOptionChainRangeAsync(
                underlyingSymbol, fromMaturityDate, throughMaturityDate, cancellationToken),
            onCompleted);

    public Task<ServiceResult<OptionContractExpiryReadModel[]>> QueryDatabentoOptionChainRangeAsync(
        string underlyingSymbol,
        DateOnly fromMaturityDate,
        DateOnly throughMaturityDate,
        CancellationToken cancellationToken = default) =>
        _queryApi.GetDatabentoOptionChainRangeAsync(
            underlyingSymbol, fromMaturityDate, throughMaturityDate, cancellationToken);

    public Task<ServiceResult<FuturesOptionContractReadModel[]>> QueryDatabentoOptionChainAsync(
        string underlyingSymbol,
        string providerRoot,
        DateOnly maturityDate,
        CancellationToken cancellationToken = default) =>
        _queryApi.GetDatabentoOptionChainAsync(
            underlyingSymbol, providerRoot, maturityDate, cancellationToken);

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

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task GetValueDateAsync(Func<DateOnly?, Task> onCompleted)
        => ExecuteAsync(_queryApi.GetValueDateAsync, vm => onCompleted(vm?.Value));

    /// <summary>Gets operational and live-session dates from the authoritative market-session policy.</summary>
    public Task GetMarketSessionAsync(Func<MarketSessionReadModel, Task> onCompleted)
        => ExecuteAsync(_queryApi.GetMarketSessionAsync, onCompleted);

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

    /// <summary>Executes or exposes a documented UI service operation.</summary>
    public Task GetTradingDaysAsync(
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType,
        Func<int, Task> onCompleted)
        => ExecuteAsync(
            () => _queryApi.GetTradingDaysAsync(startDate, endDate, marketType, currencyType),
            vm => onCompleted(vm.Value));

    /// <summary>
    /// return current traded futures eod data
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="onCompleted"></param>
    /// <returns></returns>
    public async Task GetCurrentFuturesEodDataAsync(DateOnly valueDate, Action<FuturesEodDataV2ReadModel> onCompleted)
    {
        var serviceResult = await _queryApi.GetOnTheRunFuturesContractAsync("ES");
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

    /// <summary>Gets whether a yield curve already exists for the supplied value date.</summary>
    /// <param name="valueDate">The value date to check.</param>
    /// <returns><see langword="true"/> when a stored curve exists; otherwise <see langword="false"/>.</returns>
    public async Task<bool> YieldCurveRateExistsValueAsync(DateOnly valueDate)
    {
        var result = await _queryApi.YieldCurveRateExistsAsync(valueDate);
        if (result.Success && result.Value is not null)
            return result.Value.Value;
        RaiseError(result.ErrorCode, result.ErrorMessage);
        return false;
    }
}
