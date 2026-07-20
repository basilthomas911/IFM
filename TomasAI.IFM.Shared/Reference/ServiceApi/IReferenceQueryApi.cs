using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.Reference.ServiceApi
{
    public interface IReferenceQueryApi
    {
        Task<ServiceResult<LookupTypeCollection>> GetMarketDataDefinitionTypesAsync();
        Task<ServiceResult<LookupTypeCollection>> GetReferenceDataDefinitionTypesAsync();
        Task<ServiceResult<LookupTypeCollection>> GetSystemAdminFunctionTypesAsync();
        Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync();
        Task<ServiceResult<LookupTypeCollection>> GetLookupTypesAsync(string lookupTypeName);
        Task<ServiceResult<string[]>> GetLookupTypeNamesAsync();
        Task<ServiceResult<LookupTypeShortCodeReadModel[]>> GetLookupTypeShortCodesAsync(string lookupTypeName);
        Task<ServiceResult<ScalarReadModel<int>>> GetNextSeedIdAsync(string seedType);
        Task<ServiceResult<ScalarReadModel<int>>> GetCurrentSeedIdAsync(string seedType);
        Task<ServiceResult<DefaultFuturesContractDefinitionsReadModel>> GetDefaultFuturesContractDefinitionsAsync();
        Task<ServiceResult<FuturesOptionStrikePriceReadModel>> GetFuturesOptionStrikePriceDefinitionsAsync();
        Task<ServiceResult<ScalarReadModel<bool>>> LookupTypeShortCodeExistsAsync(string lookupTypeName, string shortCode);
        Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync(DateTime todaysDate,EconomicCalendarViewType calendarType, string countryCode);
        Task<ServiceResult<EconomicCalendarReadModel[]>> GetEconomicCalendarsAsync();
        Task<ServiceResult<EconomicCalendarReadModel[]>> GetExternalEconomicCalendarsAsync();
        Task<ServiceResult<string>> GetEconomicCalendarDateAsync(DateTime todaysDate, EconomicCalendarViewType calendarType);
        Task<ServiceResult<EconomicCalendarCountryCodeReadModel[]>> GetEconomicCalendarCountryCodesAsync();
        Task<ServiceResult<MDIForwardLossRatioReadModel[]>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
    }
}
