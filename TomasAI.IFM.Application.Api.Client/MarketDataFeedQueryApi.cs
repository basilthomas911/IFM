using System;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// REST API client for MarketDataFeed queries that delegates to an <see cref="IQueryServiceApi"/>.
/// Mirrors the pattern used by <see cref="FundQueryApi"/>.
/// </summary>
public class MarketDataFeedQueryApi(IQueryServiceApi querySvc) : IMarketDataFeedQueryApi
{
    readonly IQueryServiceApi _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// Return last futures tick data for a contract on a specific value date.
    /// </summary>
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetLastFuturesTickDataParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesTickDataV2ReadModel>(MarketDataFeedQueryUriPath.GetLastFuturesTickData, qryParam, 1015);
    }

    /// <summary>
    /// Return last futures tick data for a contract by tick date/time.
    /// </summary>
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateTime tickDate)
    {
        var qryParam = new GetLastFuturesTickDataByTickDateParameter(contractId, tickDate);
        return await _querySvc.ExecuteQueryAsync<FuturesTickDataV2ReadModel>(MarketDataFeedQueryUriPath.GetLastFuturesTickDataByTickDate, qryParam, 1015);
    }

    /// <summary>
    /// Return last futures option tick data for a contract on a specific value date.
    /// </summary>
    public async Task<ServiceResult<FuturesOptionTickDataV2ReadModel>> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetLastFuturesOptionTickDataParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesOptionTickDataV2ReadModel>(MarketDataFeedQueryUriPath.GetLastFuturesOptionTickData, qryParam, 1014);
    }

    /// <summary>
    /// Return futures end-of-day data for the specified contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetFuturesEodDataParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesEodDataV2ReadModel>(MarketDataFeedQueryUriPath.GetFuturesEodData, qryParam, 1013);
    }

    /// <summary>
    /// Asynchronously retrieves the most recent end-of-day futures data for the specified contract and value date.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetLastFuturesEodDataParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesEodDataV2ReadModel>(MarketDataFeedQueryUriPath.GetLastFuturesEodData, qryParam, 1014);
    }

    /// <summary>
    /// Return futures EOD data in a date range for the specified contract.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel[]>> GetFuturesEodDataAsync(string contractId, DateOnly startDate, DateOnly endDate)
    {
        var qryParam = new GetFuturesEodDataByDateRangeParameter(contractId, startDate, endDate);
        return await _querySvc.ExecuteQueryAsync<FuturesEodDataV2ReadModel[]>(MarketDataFeedQueryUriPath.GetFuturesEodDataByDateRange, qryParam, 1013);
    }

    /// <summary>
    /// Return futures bar data for a contract/symbol between start and end times on a value date.
    /// </summary>
    public async Task<ServiceResult<FuturesBarDataReadModel[]>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
    {
        var qryParam = new GetFuturesBarDataParameter(contractId, symbol, valueDate, startDate, endDate);
        return await _querySvc.ExecuteQueryAsync<FuturesBarDataReadModel[]>(MarketDataFeedQueryUriPath.GetFuturesBarData, qryParam, 1012);
    }

    /// <summary>
    /// Return the last futures bar data available.
    /// </summary>
    public async Task<ServiceResult<FuturesBarDataReadModel>> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate)
    {
        var qryParam = new GetLastFuturesBarDataParameter(contractId, symbol, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesBarDataReadModel>(MarketDataFeedQueryUriPath.GetLastFuturesBarData, qryParam, 1012);
    }

    /// <summary>
    /// Return iron condor market data feed composed from the supplied option and underlying contract ids.
    /// </summary>
    public async Task<ServiceResult<IronCondorMarketDataFeedReadModel>> GetIronCondorMarketDataFeedAsync(
           string underlyingContractId,
           string shortPutOptionContractId,
           string longPutOptionContractId,
           string shortCallOptionContractId,
           string longCallOptionContractId,
           DateOnly valueDate)
    {
        var qryParam = new GetIronCondorMarketDataFeedParameter(underlyingContractId, shortPutOptionContractId, longPutOptionContractId, shortCallOptionContractId, longCallOptionContractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<IronCondorMarketDataFeedReadModel>(MarketDataFeedQueryUriPath.GetIronCondorMarketDataFeed, qryParam, 1236);
    }

    /// <summary>
    /// Return futures EOD data parameters (today + range + normal curve) for the specified contract/date.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetFuturesEodDataParametersParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesEodDataParametersReadModel>(MarketDataFeedQueryUriPath.GetFuturesEodDataParameters, qryParam, 1013);
    }

    /// <summary>
    /// Return a single futures option contract (POST because a query-for model is supplied).
    /// </summary>
    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel queryForContract)
    {
        var qryParam = new GetFuturesOptionContractParameter(contractId, queryForContract);
        return await _querySvc.PostQueryAsync<FuturesOptionContractReadModel>(MarketDataFeedQueryUriPath.GetFuturesOptionContract, qryParam, GetFuturesOptionContractQuery.ErrorId);
    }

    /// <summary>
    /// Return spread data for a short/long option pair (POST since complex model is supplied).
    /// </summary>
    public async Task<ServiceResult<FuturesOptionSpreadDataReadModel>> GetFuturesOptionSpreadDataAsync(DateOnly valueDate, DateOnly maturityDate, double assetPrice, double riskFreeRate, double timeValue, FuturesOptionContractReadModel qfShortOptionContract, FuturesOptionContractReadModel qfLongOptionContract)
    {
        var queryForOptionContracts = new FuturesOptionContractsReadModel([qfShortOptionContract, qfLongOptionContract ]);
        var qryParam = new GetFuturesOptionSpreadDataParameter(valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, queryForOptionContracts);
        return await _querySvc.PostQueryAsync<FuturesOptionSpreadDataReadModel>(MarketDataFeedQueryUriPath.GetFuturesOptionSpreadData, qryParam, GetFuturesOptionSpreadDataQuery.ErrorId);
    }

    /// <summary>
    /// Return normal curve table used for option probability computations.
    /// </summary>
    public async Task<ServiceResult<NormalCurveTableReadModel>> GetNormalCurveTableAsync()
    {
        var qryParam = new GetNormalCurveTableParameter();
        return await _querySvc.ExecuteQueryAsync<NormalCurveTableReadModel>(MarketDataFeedQueryUriPath.GetNormalCurveTable, qryParam, 1015);
    }

    /// <summary>
    /// Return VIX futures EOD history for a contract on a specified value date.
    /// </summary>
    public async Task<ServiceResult<VixFuturesEodDataReadModel[]>> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetVixFuturesEodDataParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<VixFuturesEodDataReadModel[]>(MarketDataFeedQueryUriPath.GetVixFuturesEodData, qryParam, 1014);
    }

    /// <summary>
    /// Return most recent VIX futures EOD for a contract on the specified value date.
    /// </summary>
    public async Task<ServiceResult<VixFuturesEodDataReadModel>> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
    {
        var qryParam = new GetLastVixFuturesEodDataParameter(contractId, valueDate);
        return await _querySvc.ExecuteQueryAsync<VixFuturesEodDataReadModel>(MarketDataFeedQueryUriPath.GetLastVixFuturesEodData, qryParam, 1014);
    }

    /// <summary>
    /// Return the futures risk position type for the provided value date and trade type.
    /// </summary>
    public async Task<ServiceResult<RiskPositionTypeReadModel>> GetFuturesRiskPositionTypeAsync(DateOnly valueDate, TradeType tradeType)
    {
        var qryParam = new GetFuturesRiskPositionTypeParameter(valueDate, tradeType);
        return await _querySvc.ExecuteQueryAsync<RiskPositionTypeReadModel>(MarketDataFeedQueryUriPath.GetFuturesRiskPositionType, qryParam, 1014);
    }

    /// <summary>
    /// Return futures EOD moving averages for a contract/symbol on a value date.
    /// </summary>
    public async Task<ServiceResult<FuturesEodDataMovingAveragesReadModel>> GetFuturesEodMovingAveragesAsync(string contractId, string symbol, DateOnly valueDate)
    {
        var qryParam = new GetFuturesEodMovingAveragesParameter(contractId, symbol, valueDate);
        return await _querySvc.ExecuteQueryAsync<FuturesEodDataMovingAveragesReadModel>(MarketDataFeedQueryUriPath.GetFuturesEodMovingAverages, qryParam, 1014);
    }

    /// <summary>
    /// Return a streaming request id for starting live feeds.
    /// </summary>
    public async Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync()
    {
        var qryParam = new GetStreamingRequestIdParameter();
        return await _querySvc.ExecuteQueryAsync<ScalarValue<int>>(MarketDataFeedQueryUriPath.GetStreamingRequestId, qryParam, 1016);
    }

    /// <summary>
    /// Return an option quote id used when streaming option quotes.
    /// </summary>
    public async Task<ServiceResult<ScalarValue<int>>> GetOptionQuoteIdAsync()
    {
        var qryParam = new GetOptionQuoteIdParameter();
        return await _querySvc.ExecuteQueryAsync<ScalarValue<int>>(MarketDataFeedQueryUriPath.GetOptionQuoteId, qryParam, 1016);
    }
}

