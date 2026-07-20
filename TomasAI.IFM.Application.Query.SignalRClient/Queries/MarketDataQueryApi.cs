using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Queries;

namespace TomasAI.IFM.Application.Query.SignalRClient.Queries
{
    public class MarketDataQueryApi : IMarketDataQueryApi
    {
        private IQueryService _querySvc;

        public MarketDataQueryApi(IQueryService querySvc)
        {
            _querySvc = querySvc;
        }

        public async Task<ServiceResult<FuturesContractViewModel>> GetCurrentTradedFuturesContractAsync()
            => await _querySvc.ExecuteQueryAsync<FuturesContractViewModel>(new GetCurrentTradedFuturesContractQuery {});

        public async Task<ServiceResult<FuturesContractViewModel>> GetFuturesContractAsync(string contractId)
            => await _querySvc.ExecuteQueryAsync<FuturesContractViewModel>(new GetFuturesContractQuery { ContractId = contractId });

        public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId)
            => await _querySvc.ExecuteQueryAsync<FuturesOptionContractReadModel>(new GetFuturesOptionContractQuery { ContractId = contractId });

        public async Task<ServiceResult<FuturesContractViewModel[]>> GetFuturesContractsAsync(string symbol)
            => await _querySvc.ExecuteQueryAsync<FuturesContractViewModel[]>(new GetFuturesContractsQuery { Symbol = symbol });

        public async Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol)
            => await _querySvc.ExecuteQueryAsync<FuturesOptionContractReadModel[]>(new GetFuturesOptionContractsQuery { Symbol = symbol });

        public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
            => await _querySvc.ExecuteQueryAsync<YieldCurveRateReadModel>(new GetLastYieldCurveRateQuery());

        public async Task<ServiceResult<ReturnRateViewModel>> GetLastRateOfReturnAsync(string symbol, DateTime valueDate)
            => await _querySvc.ExecuteQueryAsync<ReturnRateViewModel>(new GetLastRateOfReturnQuery { Symbol = symbol});

        public async Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType)
            => await _querySvc.ExecuteQueryAsync<ScalarReadModel<int>>(new GetTradingDaysQuery {
                StartDate = startDate,
                EndDate = endDate,
                MarketType = marketType,
                CurrencyType = currencyType});

        public async Task<ServiceResult<DateTime[]>> GetTradingDatesAsync(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType)
           => await _querySvc.ExecuteQueryAsync<DateTime[]>(new GetTradingDatesQuery {
               StartDate = startDate,
               EndDate = endDate,
               MarketType = marketType,
               CurrencyType = currencyType
           });

        public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(DateTime startDate, DateTime endDate)
            => await _querySvc.ExecuteQueryAsync<YieldCurveRateReadModel[]>(new GetYieldCurveRatesQuery {
                StartDate = startDate,
                EndDate = endDate
            });

        public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
             => await _querySvc.ExecuteQueryAsync<YieldCurveRateYearsReadModel>(new GetYieldCurveRateYearsQuery());

        public async Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateTime valueDate)
             => await _querySvc.ExecuteQueryAsync<ScalarReadModel<bool>>(new GetYieldCurveRateExistsQuery {ValueDate = valueDate});

        public async Task<ServiceResult<ScalarReadModel<DateTime>>> GetValueDateAsync()
             => await _querySvc.ExecuteQueryAsync<ScalarReadModel<DateTime>>(new GetValueDateQuery{ });

        public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync()
             => await _querySvc.ExecuteQueryAsync<YieldCurveRateReadModel[]>(new GetExternalYieldCurveRatesQuery { });

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
             => await _querySvc.ExecuteQueryAsync<IronCondorMarketDataReadModel>(new GetIronCondorMarketDataQuery {
                 UnderlyingContractId = underlyingContractId,
                 ShortPutOptionContractId = shortPutOptionContractId,
                 LongPutOptionContractId = longPutOptionContractId,
                 ShortCallOptionContractId = shortCallOptionContractId,
                 LongCallOptionContractId = longCallOptionContractId,
                 StartDate = startDate,
                 EndDate = endDate,
                 MarketType = marketType,
                 CurrencyType = currencyType
             });
    }
}
