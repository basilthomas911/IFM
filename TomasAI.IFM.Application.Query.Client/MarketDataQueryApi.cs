using System;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.Client
{
    public class MarketDataQueryApi : IMarketDataQueryApi
    {
        readonly IQueryService _querySvc;
        readonly string _controller;

        public MarketDataQueryApi(IQueryService querySvc)
        {
            _querySvc = IsArgumentNull.Set(querySvc);
            _controller = "MarketData";
        }

        public async Task<ServiceResult<FuturesContractViewModel>> GetCurrentTradedFuturesContractAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetCurrentTradedFuturesContractQuery { }, _controller);

        public async Task<ServiceResult<FuturesContractViewModel[]>> GetCurrentlyTradedFuturesContractsAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetCurrentlyTradedFuturesContractsQuery { }, _controller);

        public async Task<ServiceResult<FuturesContractViewModel>> GetFuturesContractAsync(string contractId)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesContractQuery { ContractId = contractId }, _controller);

        public async Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId)
        { 
             var serviceResult = await _querySvc.ExecuteApiQueryAsync(new GetFuturesContractQuery { ContractId = contractId }, _controller);
             if (serviceResult.Success && serviceResult.Value is not null)
             {
                var futuresContract = serviceResult.Value;
                return new ServiceOk<string>(futuresContract.Symbol);
             }
             return new ServiceFailed<string>(serviceResult.ErrorCode, serviceResult.ErrorMessage);
        }

        public async Task<ServiceResult<FuturesTradeSignalViewModel>> GetFuturesTradeSignalAsync(string contractId, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTradeSignalQuery { ContractId = contractId, ValueDate = valueDate }, _controller);

        public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesOptionContractQuery { ContractId = contractId }, _controller);

        public async Task<ServiceResult<FuturesContractViewModel[]>> GetFuturesContractsAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesContractsQuery { }, _controller);

        public async Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesOptionContractsQuery { Symbol = symbol }, _controller);

        public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
            => await _querySvc.ExecuteApiQueryAsync(new GetLastYieldCurveRateQuery(), _controller  );

        public async Task<ServiceResult<ReturnRateViewModel>> GetLastRateOfReturnAsync(string symbol, DateTime valueDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetLastRateOfReturnQuery { Symbol = symbol}, _controller);

        public async Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType)
            => await _querySvc.ExecuteApiQueryAsync(new GetTradingDaysQuery {
                StartDate = startDate,
                EndDate = endDate,
                MarketType = marketType,
                CurrencyType = currencyType}, _controller);

        public async Task<ServiceResult<DateTime[]>> GetTradingDatesAsync(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType)
           => await _querySvc.ExecuteApiQueryAsync(new GetTradingDatesQuery {
               StartDate = startDate,
               EndDate = endDate,
               MarketType = marketType,
               CurrencyType = currencyType
           }, _controller);

        public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(DateTime startDate, DateTime endDate)
            => await _querySvc.ExecuteApiQueryAsync(new GetYieldCurveRatesQuery {
                StartDate = startDate,
                EndDate = endDate
            },_controller);

        public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
             => await _querySvc.ExecuteApiQueryAsync(new GetYieldCurveRateYearsQuery(), _controller);

        public async Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateTime valueDate)
             => await _querySvc.ExecuteApiQueryAsync(new GetYieldCurveRateExistsQuery {ValueDate = valueDate}, _controller);

        public async Task<ServiceResult<ScalarReadModel<DateTime>>> GetValueDateAsync()
             => await _querySvc.ExecuteApiQueryAsync(new GetValueDateQuery{ }, _controller);

        public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync()
             => await _querySvc.ExecuteApiQueryAsync(new GetExternalYieldCurveRatesQuery { }, _controller);

        public async Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(
                string underlyingContractId,
                string shortPutOptionContractId,
                string longPutOptionContractId,
                string shortCallOptionContractId,
                string longCallOptionContractId,
                DateTime startDate,
                DateTime endDate,
                MarketType marketType,
                CurrencyType currencyType)
             => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorMarketDataQuery {
                 UnderlyingContractId = underlyingContractId,
                 ShortPutOptionContractId = shortPutOptionContractId,
                 LongPutOptionContractId = longPutOptionContractId,
                 ShortCallOptionContractId = shortCallOptionContractId,
                 LongCallOptionContractId = longCallOptionContractId,
                 StartDate = startDate,
                 EndDate = endDate,
                 MarketType = marketType,
                 CurrencyType = currencyType
             }, _controller);

        /// <summary>
        /// return futures option contract id's
        /// </summary>
        /// <param name="contractIds">query for these contract id's</param>
        /// <returns></returns>
        public async Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds)
            => await _querySvc.ExecuteApiQueryAsync(new GetFuturesOptionContractIdsQuery { ContractIds = contractIds }, _controller);
    }
}
