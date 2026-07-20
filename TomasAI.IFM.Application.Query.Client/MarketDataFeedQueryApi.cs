using System;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Trade;
using RestSharp.Validation;
using System.Diagnostics.Contracts;

namespace TomasAI.IFM.Application.Query.Client
{
    
    public class MarketDataFeedQueryApi : IMarketDataFeedQueryApi
    {
        const  string Controller = "MarketDataFeed";
        readonly IQueryService _querySvc;

        public MarketDataFeedQueryApi(IQueryService querySvc)
        {
            _querySvc = IsArgumentNull.Set(querySvc);
        }

        /// <summary>
        /// return last futures tick data
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesTickDataViewModel>> GetLastFuturesTickDataAsync(string contractId)
            => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesTickDataQuery { ContractId = contractId }, Controller);

        /// <summary>
        /// return last futures tick data by tick date
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="tickDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesTickDataViewModel>> GetLastFuturesTickDataAsync(string contractId, DateTime tickDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesTickDataByTickDateQuery { ContractId = contractId , TickDate = tickDate}, Controller);

        /// <summary>
        /// return last ftures option tick data
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesOptionTickDataViewModel>> GetLastFuturesOptionTickDataAsync(string contractId)
            => await _querySvc.ExecuteApiQueryAsync(new GetLastFuturesOptionTickDataQuery { ContractId = contractId }, Controller);

        /// <summary>
        /// return futures eod data for selected symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesEodDataViewModel>> GetFuturesEodDataAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesEodDataQuery { ContractId = contractId, ValueDate = valueDate }, Controller);

        /// <summary>
        /// return futures eod data by date range
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesEodDataViewModel[]>> GetFuturesEodDataAsync(string contractId, DateTime startDate, DateTime endDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesEodDataByDateRangeQuery { ContractId = contractId, StartDate = startDate, EndDate = endDate }, Controller);

        /// <summary>
        /// return futures bar data by date range
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="symbol"></param>
        /// <param name="valueDate"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesBarDataReadModel[]>> GetFuturesBarDataAsync(string contractId, string symbol, DateTime valueDate, DateTime startDate, DateTime endDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesBarDataQuery { ContractId = contractId, Symbol = symbol, ValueDate = valueDate, StartDate = startDate, EndDate = endDate }, Controller);

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
               string longCallOptionContractId)
            => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorMarketDataFeedQuery
            {
                UnderlyingContractId = underlyingContractId,
                ShortPutOptionContractId = shortPutOptionContractId,
                LongPutOptionContractId = longPutOptionContractId,
                ShortCallOptionContractId = shortCallOptionContractId,
                LongCallOptionContractId = longCallOptionContractId
            }, Controller);

        /// <summary>
        /// return futures eod data parameters
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesEodDataParametersQuery {
                    ContractId = contractId,
                    ValueDate = valueDate
                }, Controller);

        /// <summary>
        /// return futures option contract via market data api
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="queryForContract"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel queryForContract)
             => await _querySvc.PostApiQueryAsync(new GetFuturesOptionContractQuery {
                 ContractId = contractId,
                 QueryForContract = queryForContract
             }, Controller);

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
        public async Task<ServiceResult<FuturesOptionSpreadDataReadModel>> GetFuturesOptionSpreadDataAsync(DateTime valueDate, DateTime maturityDate, 
            double assetPrice, double riskFreeRate, double timeValue, FuturesOptionContractReadModel qfShortOptionContract, FuturesOptionContractReadModel qfLongOptionContract)
             => await _querySvc.PostApiQueryAsync(new GetFuturesOptionSpreadDataQuery {
                 ValueDate = valueDate,
                 MaturityDate = maturityDate,
                 AssetPrice = assetPrice,
                 RiskFreeRate = riskFreeRate,
                 TimeValue = timeValue,
                 QueryForShortOptionContract = qfShortOptionContract,
                 QueryForLongOptionContract = qfLongOptionContract
             }, Controller);

        /// <summary>
        /// return normal curve table
        /// </summary>
        /// <returns></returns>
        public async Task<ServiceResult<NormalCurveTableReadModel>> GetNormalCurveTableAsync() 
            => await _querySvc.ExecuteApiQueryAsync(new GetNormalCurveTableQuery {}, Controller);

        /// <summary>
        /// return vix futures eod data
        /// </summary>
        /// <param name="contractId">vix futures contract id</param>
        /// <param name="valueDate">current value date</param>
        /// <returns></returns>
        public async Task<ServiceResult<VixFuturesEodDataReadModel[]>> GetVixFuturesEodDataAsync(string contractId, DateTime valueDate) 
            => await _querySvc.ExecuteApiQueryAsync(new GetVixFuturesEodDataQuery  {
                ContractId = contractId,
                ValueDate = valueDate,
            }, Controller );

        /// <summary>
        /// return last vix futures eod data
        /// </summary>
        /// <param name="valueDate">current value date</param>
        /// <returns></returns>
        public async Task<ServiceResult<VixFuturesEodDataReadModel>> GetLastVixFuturesEodDataAsync(DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetLastVixFuturesEodDataQuery {
                ValueDate = valueDate,
            }, Controller);

        /// <summary>
        /// return futures risk position type
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="tradeType"></param>
        /// <returns></returns>
        public async Task<ServiceResult<RiskPositionTypeReadModel>> GetFuturesRiskPositionTypeAsync(DateTime valueDate, TradeType tradeType)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesRiskPositionTypeQuery {
                ValueDate = valueDate,
                TradeType = tradeType,
            }, Controller);

        /// <summary>
        /// return futures eod moving averages
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesEodMovingAveragesViewModel>> GetFuturesEodMovingAveragesAsync(string symbol, DateTime valueDate)
                   => await _querySvc.ExecuteApiQueryAsync(new GetFuturesEodMovingAveragesQuery {
                       Symbol = symbol,
                      ValueDate  = valueDate,
                   }, Controller);

        /// <summary>
        /// return streaming id
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<ScalarValue<int>>> GetStreamingRequestIdAsync(Guid streamId)
                    => await _querySvc.ExecuteApiQueryAsync(new GetStreamingRequestIdQuery { StreamId = streamId }, Controller);

    }
}
