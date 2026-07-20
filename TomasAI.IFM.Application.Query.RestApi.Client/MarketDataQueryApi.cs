using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Query.Client;

public class MarketDataQueryApi(IQueryService querySvc) : IMarketDataQueryApi
{
    readonly IQueryService _querySvc = IsArgumentNull.Set(querySvc);
    readonly string _controller = "MarketData";

    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(string symbol)
        => await _querySvc.ExecuteApiQueryAsync(new GetCurrentlyTradedFuturesContractQuery (symbol), _controller);

    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(string symbol)
        => await _querySvc.ExecuteApiQueryAsync(new GetCurrentlyTradedFuturesContractsQuery (symbol), _controller);

    public async Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesContractQuery (contractId ), _controller);

    public async Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId)
    { 
         var serviceResult = await _querySvc.ExecuteApiQueryAsync(new GetFuturesContractQuery (contractId), _controller);
         if (serviceResult.Success && serviceResult.Value is not null)
         {
            var futuresContract = serviceResult.Value;
            return new ServiceOk<string>(futuresContract.Symbol);
         }
         return new ServiceFailed<string>(serviceResult.ErrorCode, serviceResult.ErrorMessage);
    }

    public async Task<ServiceResult<FuturesTradeSignalV2ReadModel>> GetFuturesTradeSignalAsync(string contractId, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesTradeSignalQuery (contractId, valueDate), _controller);

    public async Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesOptionContractQuery (contractId), _controller);

    public async Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync()
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesContractsQuery (), _controller);

    public async Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesOptionContractsQuery(symbol), _controller);

    public async Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync()
        => await _querySvc.ExecuteApiQueryAsync(new GetLastYieldCurveRateQuery(), _controller  );

    public async Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(string symbol, DateOnly valueDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetLastRateOfReturnQuery( symbol, valueDate), _controller);

    public async Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
        => await _querySvc.ExecuteApiQueryAsync(new GetTradingDaysQuery (startDate, endDate, marketType, currencyType), _controller);

    public async Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType)
       => await _querySvc.ExecuteApiQueryAsync(new GetTradingDatesQuery(startDate, endDate, marketType, currencyType), _controller);

    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate)
        => await _querySvc.ExecuteApiQueryAsync(new GetYieldCurveRatesQuery ( startDate,endDate),_controller);

    public async Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync()
         => await _querySvc.ExecuteApiQueryAsync(new GetYieldCurveRateYearsQuery(), _controller);

    public async Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateOnly valueDate)
         => await _querySvc.ExecuteApiQueryAsync(new GetYieldCurveRateExistsQuery(valueDate), _controller);

    public async Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync()
         => await _querySvc.ExecuteApiQueryAsync(new GetValueDateQuery(), _controller);

    public async Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync()
         => await _querySvc.ExecuteApiQueryAsync(new GetExternalYieldCurveRatesQuery(), _controller);

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
         => await _querySvc.ExecuteApiQueryAsync(new GetIronCondorMarketDataQuery (
             underlyingContractId, 
             shortPutOptionContractId,
             longPutOptionContractId,
             shortCallOptionContractId,
             longCallOptionContractId,
             startDate,
             endDate,
             marketType,
             currencyType), _controller);

    /// <summary>
    /// return futures option contract id's
    /// </summary>
    /// <param name="contractIds">query for these contract id's</param>
    /// <returns></returns>
    public async Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds)
        => await _querySvc.ExecuteApiQueryAsync(new GetFuturesOptionContractIdsQuery(contractIds), _controller);
}
