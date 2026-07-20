using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Query.SignalRClient.Queries
{
    
    public class MarketDataFeedQueryApi : IMarketDataFeedQueryApi
    {
        private IQueryService _querySvc;
        private Dictionary<(string, DateTime, DateTime), FuturesEodDataViewModel[]> _futuresEodDataMap;
    
        public MarketDataFeedQueryApi(IQueryService querySvc)
        {
            _querySvc = querySvc;
            _futuresEodDataMap = new Dictionary<(string, DateTime, DateTime), FuturesEodDataViewModel[]>();
        }

        /// <summary>
        /// return last futures tick data
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesTickDataViewModel>> GetLastFuturesTickDataAsync(string contractId)
            => await _querySvc.ExecuteQueryAsync<FuturesTickDataViewModel>(new GetLastFuturesTickDataQuery { ContractId = contractId });

        /// <summary>
        /// return last ftures option tick data
        /// </summary>
        /// <param name="contractId"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesOptionTickDataViewModel>> GetLastFuturesOptionTickDataAsync(string contractId)
            => await _querySvc.ExecuteQueryAsync<FuturesOptionTickDataViewModel>(new GetLastFuturesOptionTickDataQuery { ContractId = contractId });

        /// <summary>
        /// return futures eod data for selected symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesEodDataViewModel>> GetFuturesEodDataAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteQueryAsync<FuturesEodDataViewModel>(new GetFuturesEodDataQuery { ContractId = contractId, ValueDate = valueDate });

        /// <summary>
        /// return futures eod data by date range
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesEodDataViewModel[]>> GetFuturesEodDataAsync(string contractId, DateTime startDate, DateTime endDate)
        {
            var futuresEodData = default(FuturesEodDataViewModel[]);
            var key = (contractId, startDate, endDate);
            if (!_futuresEodDataMap.ContainsKey(key))
            {
                var serviceResult = await _querySvc.ExecuteQueryAsync<FuturesEodDataViewModel[]>(new GetFuturesEodDataByDateRangeQuery { ContractId = contractId, StartDate = startDate, EndDate = endDate });
                if (serviceResult.Success)
                {
                    futuresEodData = serviceResult.Value;
                    _futuresEodDataMap.Add(key, futuresEodData);
                }
            }
            return _futuresEodDataMap.ContainsKey(key)
                ? new ServiceOk<FuturesEodDataViewModel[]>(_futuresEodDataMap[key]) as ServiceResult<FuturesEodDataViewModel[]>
                : new ServiceFailed<FuturesEodDataViewModel[]>(1234, $"No futures eod found between {startDate.ToString("yyyyMMdd")} and {endDate.ToString("yyyyMMdd")}") as ServiceResult<FuturesEodDataViewModel[]>;
        }

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
            => await _querySvc.ExecuteQueryAsync<IronCondorMarketDataFeedReadModel>(new GetIronCondorMarketDataFeedQuery
            {
                UnderlyingContractId = underlyingContractId,
                ShortPutOptionContractId = shortPutOptionContractId,
                LongPutOptionContractId = longPutOptionContractId,
                ShortCallOptionContractId = shortCallOptionContractId,
                LongCallOptionContractId = longCallOptionContractId
            });

        /// <summary>
        /// return futures eod data parameters
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesEodDataParametersReadModel>> GetFuturesEodDataParametersAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteQueryAsync<FuturesEodDataParametersReadModel>(new GetFuturesEodDataParametersQuery {
                    ContractId = contractId,
                    ValueDate = valueDate
                });

        /// <summary>
        /// return futures option contract via market data api
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="queryForContract"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel queryForContract)
             => await _querySvc.ExecuteQueryAsync<FuturesOptionContractReadModel>(new GetFuturesOptionContractQuery {
                 ContractId = contractId,
                 QueryForContract = queryForContract
             });

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
             => await _querySvc.ExecuteQueryAsync<FuturesOptionSpreadDataReadModel>(new GetFuturesOptionSpreadDataQuery {
                 ValueDate = valueDate,
                 MaturityDate = maturityDate,
                 AssetPrice = assetPrice,
                 RiskFreeRate = riskFreeRate,
                 TimeValue = timeValue,
                 QueryForShortOptionContract = qfShortOptionContract,
                 QueryForLongOptionContract = qfLongOptionContract
             });
    }
}
