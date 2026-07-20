using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.QueryParameters;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// Provides methods for querying market data, including contracts, rates, trading days, and iron condor analytics.
/// </summary>
/// <remarks>This API serves as a centralized interface for retrieving various types of market data information. It includes methods for querying contracts, rates, trading days, and iron condor analytics over specified time periods.</remarks>
/// <param name="querySvc"></param>
public class MarketDataQueryApi(IQueryServiceApi querySvc) : IMarketDataQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(string symbol)
    {
        var qryParam = new GetCurrentlyTradedFuturesContractParameter(symbol);
        return await _querySvc.ExecuteQueryAsync<FuturesContractV2ReadModel>(MarketDataQueryUriPath.GetCurrentlyTradedFuturesContract, qryParam, GetCurrentlyTradedFuturesContractQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(string symbol)
    {
        var qryParam = new GetCurrentlyTradedFuturesContractsParameter(symbol);
        return await _querySvc.ExecuteQueryAsync<FuturesContractV2ReadModel[]>(MarketDataQueryUriPath.GetCurrentlyTradedFuturesContracts, qryParam, GetCurrentlyTradedFuturesContractsQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId)
    {
        var qryParam = new GetFuturesContractParameter(contractId);
        return await _querySvc.ExecuteQueryAsync<FuturesContractV2ReadModel>(MarketDataQueryUriPath.GetFuturesContract, qryParam, GetFuturesContractQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId)
    {
        var qryParam = new GetFuturesContractSymbolParameter(contractId);
        return await _querySvc.ExecuteQueryAsync<string>(MarketDataQueryUriPath.GetFuturesContractSymbol, qryParam, 1009);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId)
    {
        var qryParam = new GetFuturesOptionContractParameter(contractId);
        return await _querySvc.ExecuteQueryAsync<FuturesOptionContractReadModel>(MarketDataQueryUriPath.GetFuturesOptionContract, qryParam, GetFuturesOptionContractQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync()
    {
        var qryParam = new GetFuturesContractsParameter();
        return await _querySvc.ExecuteQueryAsync<FuturesContractV2ReadModel[]>(MarketDataQueryUriPath.GetFuturesContracts, qryParam, GetFuturesContractsQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol)
    {
        var qryParam = new GetFuturesOptionContractsParameter(symbol);
        return await _querySvc.ExecuteQueryAsync<FuturesOptionContractReadModel[]>(MarketDataQueryUriPath.GetFuturesOptionContracts, qryParam, GetFuturesOptionContractsQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds)
    {
        var qryParam = new GetFuturesOptionContractIdsParameter(contractIds);
        return await _querySvc.PostQueryAsync<string[]>(MarketDataQueryUriPath.GetFuturesOptionContractIds, qryParam, GetFuturesOptionContractIdsQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
    {
        var qryParam = new GetLastYieldCurveRateParameter();
        return await _querySvc.ExecuteQueryAsync<YieldCurveRateReadModel>(MarketDataQueryUriPath.GetLastYieldCurveRate, qryParam, GetLastYieldCurveRateQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(string symbol, DateOnly valueDate)
    {
        var qryParam = new GetLastRateOfReturnParameter(symbol, valueDate);
        return await _querySvc.ExecuteQueryAsync<RateOfReturnReadModel>(MarketDataQueryUriPath.GetLastRateOfReturn, qryParam, 1010);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        var qryParam = new GetTradingDaysParameter(startDate, endDate, marketType, currencyType);
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(MarketDataQueryUriPath.GetTradingDays, qryParam, 1011);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
    {
        var qryParam = new GetTradingDatesParameter(startDate, endDate, marketType, currencyType);
        return await _querySvc.ExecuteQueryAsync<DateOnly[]>(MarketDataQueryUriPath.GetTradingDates, qryParam, 1011);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate)
    {
        var qryParam = new GetYieldCurveRatesParameter(startDate, endDate);
        return await _querySvc.ExecuteQueryAsync<YieldCurveRateReadModel[]>(MarketDataQueryUriPath.GetYieldCurveRates, qryParam, GetYieldCurveRatesQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync()
    {
        var qryParam = new GetExternalYieldCurveRatesParameter();
        return await _querySvc.ExecuteQueryAsync<YieldCurveRateReadModel[]>(MarketDataQueryUriPath.GetExternalYieldCurveRates, qryParam, GetExternalYieldCurveRatesQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
    {
        var qryParam = new GetYieldCurveRateYearsParameter();
        return await _querySvc.ExecuteQueryAsync<YieldCurveRateYearsReadModel>(MarketDataQueryUriPath.GetYieldCurveRateYears, qryParam, GetYieldCurveRateYearsQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateOnly valueDate)
    {
        var qryParam = new GetYieldCurveRateExistsParameter(valueDate);
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<bool>>(MarketDataQueryUriPath.YieldCurveRateExists, qryParam, GetYieldCurveRateExistsQuery.ErrorId);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync()
    {
        var qryParam = new GetValueDateParameter();
        return await _querySvc.ExecuteQueryAsync<ScalarReadModel<DateOnly>>(MarketDataQueryUriPath.GetValueDate, qryParam, 1015);
    }

    /// <inheritdoc />
    public async Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType)
    {
        var qryParam = new GetIronCondorMarketDataParameter(
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            startDate,
            endDate,
            marketType,
            currencyType);
        return await _querySvc.ExecuteQueryAsync<IronCondorMarketDataReadModel>(MarketDataQueryUriPath.GetIronCondorMarketData, qryParam, GetIronCondorMarketDataQuery.ErrorId);
    }
}
