using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.Client
{
    public class MarketDataAnalyticsQueryApi : IMarketDataAnalyticsQueryApi
    {
        readonly IQueryService _querySvc;
        readonly string _controller;

        public MarketDataAnalyticsQueryApi(IQueryService querySvc)
        {
            _querySvc = IsArgumentNull.Set(querySvc);
            _controller = "MarketDataAnalytics";
        }

        /// <summary>
        /// return futures trade signal
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesTradeSignalViewModel>> GetFuturesTradeSignalAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTradeSignalQuery { ContractId = contractId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures trade signal by symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesTradeSignalViewModel>> GetFuturesTradeSignalBySymbolAsync(string symbol, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTradeSignalBySymbolQuery { Symbol = symbol, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures trade signal ids
        /// </summary>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesTradeSignalId[]>> GetFuturesTradeSignalIdsAsync(DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTradeSignalIdsQuery { ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures rsi signal by default thirty seconds signal type
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesRsiSignalQuery { ContractId = contractId, ValueDate = valueDate, SignalType = FuturesRsiSignalType.OneMinute }, _controller);

        /// <summary>
        /// return futures rsi signal 
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <param name="signalType"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesRsiSignalReadModel>> GetFuturesRsiSignalAsync(string contractId, DateTime valueDate, FuturesRsiSignalType signalType)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesRsiSignalQuery { ContractId = contractId, ValueDate = valueDate, SignalType = signalType }, _controller);

        /// <summary>
        /// return futures trend direction from futures rsi signal
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <param name="timestamp"></param>
        /// <param name="lookbackInterval"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesTrendDirectionReadModel>> GetFuturesTrendDirectionFromRSISignalAsync(
            string contractId, DateTime valueDate, DateTime timestamp, int lookbackInterval, DateTime startTime, DateTime endTime)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTrendDirectionFromRSISignalQuery { 
                ContractId = contractId, ValueDate = valueDate, Timestamp = timestamp, LookBackInterval = lookbackInterval, StartTime = startTime, EndTime = endTime }, _controller);

        /// <summary>
        /// return futures tdi signal 
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesTdiSignalReadModel>> GetFuturesTdiSignalAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTdiSignalQuery { ContractId = contractId, ValueDate = valueDate },  _controller);

        /// <summary>
        /// return futures iti signal 
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesItiSignalViewModel>> GetFuturesItiSignalAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiSignalQuery { ContractId = contractId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures iti trend direction changed signals
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesItiSignalViewModel[]>> GetFuturesItiTrendDirectionChangedSignalsAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiTrendDirectionChangedSignalsQuery { ContractId = contractId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures iti signal data
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesItiSignalDataReadModel>> GetFuturesItiSignalDataAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiSignalDataQuery { ContractId = contractId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures iti mdi distribution
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiMDIDistributionQuery { ContractId = contractId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures iti mdi distribution by trend
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesItiMDIDistributionReadModel>> GetFuturesItiMDIDistributionByTrendAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiMDIDistributionByTrendQuery { ContractId = contractId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures iti signal mdi
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesItiSignalMDIViewModel[]>> GetFuturesItiSignalMDIAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiSignalMDIQuery { ContractId = contractId, ValueDate = valueDate }, _controller);

        /// <summary>
        /// return futures iti signal mdi by trend
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="valueDate"></param>
        /// <returns></returns>
        public async Task<ServiceResult<FuturesItiSignalMDIViewModel[]>> GetFuturesItiSignalMDIByTrendAsync(string contractId, DateTime valueDate, int groupId)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesItiSignalMDIByTrendQuery { ContractId = contractId, ValueDate = valueDate, GroupId=groupId}, _controller);


    }
}
