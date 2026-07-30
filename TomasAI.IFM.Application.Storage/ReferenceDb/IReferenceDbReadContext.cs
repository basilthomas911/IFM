using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.Storage.ReferenceDb
{
    public interface IReferenceDbReadContext 
    {
        Task<int> GetNextSeedIdAsync(string seedType);
        Task<int> GetCurrentSeedIdAsync(string seedType);
        Task<LookupTypeReadModel?> GetLookupTypeAsync(LookupTypeId lookupTypeId);
        Task<ICollection<LookupTypeReadModel>> GetLookupTypeAsync(string lookupTypeName);
        Task<ICollection<LookupTypeReadModel>> GetLookupTypesAsync();
        Task<ICollection<string>> GetLookupTypeNamesAsync();
        Task<ICollection<LookupTypeShortCodeReadModel>> GetLookupTypeShortCodesAsync(string lookupTypeName);
        Task<ICollection<ScheduledJobReadModel>> GetScheduledJobsAsync();
        Task<int> GetScheduledJobIdAsync(string jobName);
        Task<EconomicCalendarReadModel?> GetEconomicCalendarAsync(EconomicCalendarId economicCalendarId);
        Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime eventDate, string countryCode);
        Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarsAsync(DateTime startDate, DateTime endDate, string countryCode);
        Task<ICollection<EconomicCalendarReadModel>> GetEconomicCalendarAllAsync();
        Task<ICollection<EconomicCalendarCountryCodeReadModel>> GetEconomicCalendarCountryCodesAsync();
        Task<ICollection<MDIForwardLossRatioReadModel>> GetMDIForwardLossRatiosAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
    }
}
