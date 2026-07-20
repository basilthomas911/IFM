using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.ServiceApi;

public interface IMarketDataQueryApi
{
    Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(string symbol);
    Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(string symbol);
    Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId);
    Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId);
    Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId);
    Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync();
    Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol);
    Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds);
    Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync();
    Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(string symbol, DateOnly valueDate);
    Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType);
    Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType);
    Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate);
    Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync();
    Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync();
    Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateOnly valueDate);
    Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync();
    Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType );

}
