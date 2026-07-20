using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Query.Client;

public class MarketDataFeedQueryApi(IQueryService querySvc) : IMarketDataFeedQueryApi
{
    const  string Controller = "MarketDataFeed";
    readonly IQueryService _querySvc = IsArgumentNull.Set(querySvc);

    /// <summary>
    /// return last futures tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesTickDataQuery(contractId, valueDate), Controller);

    /// <summary>
    /// return last futures tick data by tick date
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="tickDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesTickDataV2ReadModel>> GetLastFuturesTickDataAsync(string contractId, DateTime tickDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesTickDataByTickDateQuery(contractId , tickDate), Controller);

    /// <summary>
    /// return last ftures option tick data
    /// </summary>
    /// <param name="contractId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesOptionTickDataV2ReadModel>> GetLastFuturesOptionTickDataAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesOptionTickDataQuery (contractId, valueDate), Controller);

    /// <summary>
    /// return futures eod data for selected symbol
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesEodDataQuery (contractId, valueDate), Controller);

    /// <summary>
    /// Asynchronously retrieves the most recent end-of-day futures data.
    /// </summary>
    /// <remarks>This method queries the underlying service to obtain the latest available futures data at the
    /// end of the trading day. The result encapsulates the data in a service result object, which includes status
    /// information and any potential errors.</remarks>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{T}"/> of
    /// type <see cref="FuturesEodDataV2ReadModel"/> representing the latest end-of-day futures data.</returns>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel>> GetLastFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesEodDataQuery(contractId, valueDate), Controller);

    /// <summary>
    /// return futures eod data by date range
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesEodDataV2ReadModel[]>> GetFuturesEodDataAsync(string contractId, DateOnly startDate, DateOnly endDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesEodDataByDateRangeQuery (contractId, startDate, endDate), Controller);

    /// <summary>
    /// return futures bar data by date range
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesBarDataReadModel[]>> GetFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate, DateTime startDate, DateTime endDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesBarDataQuery (contractId, symbol, valueDate, startDate, endDate), Controller);

    /// <summary>
    /// get last futures bar data
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesBarDataReadModel>> GetLastFuturesBarDataAsync(string contractId, string symbol, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesBarDataQuery(contractId, symbol, valueDate), Controller);

    /// <summary>
    /// return iron condor market data feed
    /// </summary>
    /// <param name="underlyingContractId"></param>
    /// <param name="shortPutOptionContractId"></param>
    /// <param name="longPutOptionContractId"></param>
    /// <param name="shortCallOptionContractId"></param>
    /// <param name="longCallOptionContractId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<IronCondorMarketDataFeedReadModel>> GetIronCondorMarketDataFeedAsync(
           string underlyingContractId,
           string shortPutOptionContractId,
           string longPutOptionContractId,
           string shortCallOptionContractId,
           string longCallOptionContractId,
           DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorMarketDataFeedQuery
        (
            underlyingContractId,
            shortPutOptionContractId,
            longPutOptionContractId,
            shortCallOptionContractId,
            longCallOptionContractId,
            valueDate
        ), Controller);

    /// <summary>
    /// return futures eod data parameters
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesEodDataParametersQuery (contractId,valueDate), Controller);

    /// <summary>
    /// return futures option contract via market data api
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="queryForContract"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel queryForContract)
         => await _querySvc.PostApiQueryAsync(new GetFuturesOptionContractQuery( contractId, queryForContract), Controller);

    /// <summary>
    /// return futures option spread data via market data api
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="maturityDate"></param>
    /// <param name="assetPrice"></param>
    /// <param name="riskFreeRate"></param>
    /// <param name="timeValue"></param>
    /// <param name="qfShortOptionContract"></param>
    /// <param name="qfLongOptionContract"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesOptionSpreadDataReadModel>> GetFuturesOptionSpreadDataAsync(DateOnly valueDate, DateOnly maturityDate, 
        double assetPrice, double riskFreeRate, double timeValue, FuturesOptionContractReadModel qfShortOptionContract, FuturesOptionContractReadModel qfLongOptionContract)
         => await _querySvc.PostApiQueryAsync(new GetFuturesOptionSpreadDataQuery (
             valueDate, maturityDate, assetPrice, riskFreeRate, timeValue, new([qfShortOptionContract, qfLongOptionContract])), Controller);

    /// <summary>
    /// return normal curve table
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<NormalCurveTableReadModel>> GetNormalCurveTableAsync() 
        => await _querySvc.ExecuteApiQueryAsync(new GetNormalCurveTableQuery (), Controller);

    /// <summary>
    /// return vix futures eod data
    /// </summary>
    /// <param name="contractId">vix futures contract id</param>
    /// <param name="valueDate">current value date</param>
    /// <returns></returns>
    public async Task<ServiceResult<VixFuturesEodDataReadModel[]>> GetVixFuturesEodDataAsync(string contractId, DateOnly valueDate) 
        => await _querySvc.ExecuteApiQueryAsync(new GetVixFuturesEodDataQuery ( contractId, valueDate), Controller );

    /// <summary>
    /// return last vix futures eod data
    /// </summary>
    /// <param name="contractId">vix futures contract id</param>
    /// <param name="valueDate">current value date</param>
    /// <returns></returns>
    public async Task<ServiceResult<VixFuturesEodDataReadModel>> GetLastVixFuturesEodDataAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLastVixFuturesEodDataQuery (contractId, valueDate) , Controller);

    /// <summary>
    /// return futures risk position type
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="tradeType"></param>
    /// <returns></returns>
    public async Task<ServiceResult<RiskPositionTypeReadModel>> GetFuturesRiskPositionTypeAsync(DateOnly valueDate, TradeType tradeType)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesRiskPositionTypeQuery (valueDate, tradeType), Controller);

    /// <summary>
    /// return futures eod moving averages
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="symbol"></param>
    /// <param name="valueDate"></param>
    /// <returns></returns>
    public async Task<ServiceResult<FuturesEodMovingAveragesViewModel>> GetFuturesEodMovingAveragesAsync(string contractId, string symbol, DateOnly valueDate)
               => await _querySvc.ExecuteApiQueryAsync(new GetFuturesEodMovingAveragesQuery ( contractId, symbol, valueDate), Controller);

    /// <summary>
    /// return streaming id
    /// </summary>
    /// <param name="streamId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync()
                => await _querySvc.ExecuteApiQueryAsync(new GetStreamingRequestIdQuery(), Controller);

    /// <summary>
    /// return option quote id
    /// </summary>
    /// <returns></returns>
    public async Task<ServiceResult<ScalarValue<int>>> GetOptionQuoteIdAsync()
             => await _querySvc.ExecuteApiQueryAsync(new GetOptionQuoteIdQuery(), Controller);

}
