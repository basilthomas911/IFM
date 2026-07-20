using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Models
{
    public class MarketDataQueryModel : BaseModel<MarketDataQueryModel>
    {
        readonly IMarketDataQueryApi _queryApi;
        readonly IMarketDataFeedQueryApi _queryFeedApi;

        /// <summary>
        /// create market data query model
        /// </summary>
        /// <param name="appRoot"></param>
        public MarketDataQueryModel(IMarketDataQueryApi queryApi, IMarketDataFeedQueryApi queryFeedApi)
        {
            _queryApi = queryApi ?? throw new ArgumentNullException(nameof(queryApi));
            _queryFeedApi = queryFeedApi ?? throw new ArgumentNullException(nameof(queryFeedApi));
        }

        /// <summary>
        /// load futures contract
        /// </summary>
        /// <param name="contractId"></param>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public async Task GetFuturesContractAsync(string contractId, Action<FuturesContractViewModel> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFuturesContractAsync(contractId), onCompleted);

        /// <summary>
        /// load futures contract
        /// </summary>
        /// <param name="onCompleted"></param>
        public async Task GetFuturesContractsAsync(Action<FuturesContractViewModel[]> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetFuturesContractsAsync(), onCompleted);

        /// <summary>
        /// load currently traded futures contract
        /// </summary>
        /// <param name="onCompleted"></param>
        public async Task GetCurrentlyTradedFuturesContractAsync(Action<FuturesContractViewModel> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetCurrentTradedFuturesContractAsync(), onCompleted);

        /// <summary>
        /// load currently traded futures contract
        /// </summary>
        /// <param name="onCompleted"></param>
        public async Task GetCurrentlyTradedFuturesContractsAsync(Action< ICollection<FuturesContractViewModel>> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetCurrentlyTradedFuturesContractsAsync(), onCompleted);
        
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
                var timePeriods = new List<string> { "Current Month" };
                var modelYears = serviceResult.Value;
                if (modelYears != null)
                    foreach (var e in modelYears.Value)
                        timePeriods.Add($"{e}");
                onCompleted?.Invoke(timePeriods.ToArray());
            }
            else
                RaiseError(serviceResult.ErrorCode, serviceResult.ErrorMessage);
        }

        /// <summary>
        /// get value date
        /// </summary>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public async Task GetValueDateAsync(Action<DateTime?> onCompleted)
            => await ExecuteAsync(_queryApi.GetValueDateAsync, vm => onCompleted(vm?.Value));

        /// <summary>
        /// get yield curve rates by date range
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public async Task GetYieldCurveRatesAsync(DateTime startDate, DateTime endDate, Action<YieldCurveRateReadModel[]> onCompleted)
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
        public async Task GetTradingDatesAsync(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType, Action<DateTime[]> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetTradingDatesAsync(startDate, endDate, marketType, currencyType), onCompleted);

        /// <summary>
        /// get trading days by date range
        /// </summary>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <param name="marketType"></param>
        /// <param name="currencyType"></param>
        /// <param name="onCompleted"></param>
        public async Task GetTradingDaysAsync(DateTime startDate, DateTime endDate, MarketType marketType, CurrencyType currencyType, Action<int> onCompleted)
            => await ExecuteAsync(() => _queryApi.GetTradingDaysAsync(startDate, endDate, marketType, currencyType), vm => onCompleted(vm.Value));

        /// <summary>
        /// return current traded futures eod data
        /// </summary>
        /// <param name="valueDate"></param>
        /// <param name="onCompleted"></param>
        /// <returns></returns>
        public async Task GetCurrentFuturesEodDataAsync(DateTime valueDate, Action<FuturesEodDataViewModel> onCompleted)
        {
            var serviceResult = await _queryApi.GetCurrentTradedFuturesContractAsync();
            if (serviceResult.Success)
            {
                var contractId = serviceResult.Value.ContractId;
                var serviceResult2 = await _queryFeedApi.GetFuturesEodDataAsync(contractId, valueDate);
                if (serviceResult2.Success)
                    onCompleted?.Invoke(serviceResult2.Value);
                else
                    RaiseError(serviceResult2.ErrorCode, serviceResult2.ErrorMessage);
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
        public async Task YieldCurveRateExistsAsync(DateTime valueDate, Action<ServiceResult<ScalarReadModel<bool>>> onCompleted)
        {
            var serviceResult = await _queryApi.YieldCurveRateExistsAsync(valueDate);
            onCompleted?.Invoke(serviceResult);
        }
    }
}
