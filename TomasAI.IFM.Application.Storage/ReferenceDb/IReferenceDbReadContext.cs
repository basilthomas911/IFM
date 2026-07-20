using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataAnalytics;

namespace TomasAI.IFM.Application.Storage.ReferenceDb
{
    public interface IReferenceDbReadContext : IReferenceDbContext
    {
        Task<int> GetNextSeedIdAsync(string seedType);
        Task<int> GetCurrentSeedIdAsync(string seedType);
        Task<LookupTypeReadModel> GetLookupTypeAsync(LookupTypeId lookupTypeId);
        Task<IReadOnlyList<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName);
        Task<IReadOnlyList<LookupTypeReadModel>> GetLookupTypesAsync();
        Task<IReadOnlyList<string>> GetLookupTypeNamesAsync();
        Task<IReadOnlyList<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName);
        Task<IReadOnlyList<ScheduledJobReadModel>> GetScheduledJobsAsync();
        Task<int> GetScheduledJobIdAsync(string jobName);
        Task<IReadOnlyList<StrikePriceVolatilityReadModel>> GetStrikePriceVolatilityAsync(string symbol, TradeType tradeType);
        Task<EconomicCalendarReadModel> GetEconomicCalendarAsync(EconomicCalendarId economicCalendarId);
        Task<IReadOnlyList<EconomicCalendarReadModel>> GetEconomicCalendarAsync(DateTime eventDate, string countryCode);
        Task<IReadOnlyList<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode);
        Task<IReadOnlyList<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync();
        Task<IReadOnlyList<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync();
        Task<IReadOnlyList<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
    }
}
