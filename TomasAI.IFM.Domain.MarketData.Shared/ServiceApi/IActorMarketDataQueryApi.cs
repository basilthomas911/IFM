using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;

/// <summary>
/// Defines Market Data queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorMarketDataQueryApi : IMarketDataQueryApi
{
    Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode, CancellationToken cancellationToken);
    Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(CancellationToken cancellationToken);
    Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync(CancellationToken cancellationToken);
    Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarType, CancellationToken cancellationToken);
    Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken);
    Task<ServiceResult<FuturesContractV2ReadModel>> GetCurrentlyTradedFuturesContractAsync(string symbol, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesContractV2ReadModel[]>> GetCurrentlyTradedFuturesContractsAsync(string symbol, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesContractV2ReadModel>> GetFuturesContractAsync(string contractId, CancellationToken cancellationToken);
    Task<ServiceResult<string>> GetFuturesContractSymbolAsync(string contractId, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesOptionContractReadModel>> GetFuturesOptionContractAsync(string contractId, CancellationToken cancellationToken);
    Task<ServiceResult<FuturesContractV2ReadModel[]>> GetFuturesContractsAsync(CancellationToken cancellationToken);
    Task<ServiceResult<FuturesOptionContractReadModel[]>> GetFuturesOptionContractsAsync(string symbol, CancellationToken cancellationToken);
    Task<ServiceResult<string[]>> GetFuturesOptionContractIdsAsync(string[] contractIds, CancellationToken cancellationToken);
    Task<ServiceResult<YieldCurveRateReadModel>> GetLastYieldCurveRateAsync(CancellationToken cancellationToken);
    Task<ServiceResult<RateOfReturnReadModel>> GetLastRateOfReturnAsync(string symbol, DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<ScalarReadModel<int>>> GetTradingDaysAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, CancellationToken cancellationToken);
    Task<ServiceResult<DateOnly[]>> GetTradingDatesAsync(DateOnly startDate, DateOnly endDate, MarketType marketType, CurrencyType currencyType, CancellationToken cancellationToken);
    Task<ServiceResult<YieldCurveRateReadModel[]>> GetYieldCurveRatesAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken);
    Task<ServiceResult<YieldCurveRateReadModel[]>> GetExternalYieldCurveRatesAsync(CancellationToken cancellationToken);
    Task<ServiceResult<YieldCurveRateYearsReadModel>> GetYieldCurveRateYearsAsync(CancellationToken cancellationToken);
    Task<ServiceResult<ScalarReadModel<bool>>> YieldCurveRateExistsAsync(DateOnly valueDate, CancellationToken cancellationToken);
    Task<ServiceResult<ScalarReadModel<DateOnly>>> GetValueDateAsync(CancellationToken cancellationToken);
    Task<ServiceResult<IronCondorMarketDataReadModel>> GetIronCondorMarketDataAsync(
        string underlyingContractId,
        string shortPutOptionContractId,
        string longPutOptionContractId,
        string shortCallOptionContractId,
        string longCallOptionContractId,
        DateOnly startDate,
        DateOnly endDate,
        MarketType marketType,
        CurrencyType currencyType,
        CancellationToken cancellationToken);
}
