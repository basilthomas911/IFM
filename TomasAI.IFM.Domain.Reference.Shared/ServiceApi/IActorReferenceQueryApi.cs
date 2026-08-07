using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

/// <summary>
/// Defines Reference queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorReferenceQueryApi : IReferenceQueryApi
{
    Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync(CancellationToken cancellationToken);
    Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync(CancellationToken cancellationToken);
    Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync(CancellationToken cancellationToken);
    Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(CancellationToken cancellationToken);
    Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(string lookupTypeName, CancellationToken cancellationToken);
    Task<ServiceResult<string[]>> GetLookupTypeNamesAsync(CancellationToken cancellationToken);
    Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(string lookupTypeName, CancellationToken cancellationToken);
    Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType, CancellationToken cancellationToken);
    Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType, CancellationToken cancellationToken);
    Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync(CancellationToken cancellationToken);
    Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync(CancellationToken cancellationToken);
    Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode, CancellationToken cancellationToken);
    Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(DateTime todaysDate, EconomicCalendarViewType calendarType, string countryCode, CancellationToken cancellationToken);
    Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(CancellationToken cancellationToken);
    Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync(CancellationToken cancellationToken);
    Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarType, CancellationToken cancellationToken);
    Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync(CancellationToken cancellationToken);
    Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType, CancellationToken cancellationToken);
}
