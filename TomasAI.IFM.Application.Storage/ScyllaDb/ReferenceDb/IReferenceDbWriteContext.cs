using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Storage.ScyllaDb.ReferenceDb;

public interface IReferenceDbWriteContext 
{
    Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId);
    Task DeleteScheduledJobAsync(int jobId);
    Task DeleteEconomicCalendarAsync(EconomicCalendarId id);
    Task DeleteMDIForwardLossRatioAsync(IntrinsicTimeTrendType trendDirection, TradeType tradeType);
    Task InsertLookupTypeAsync(LookupTypeReadModel lookupType);
    Task InsertScheduledJobAsync(ScheduledJobReadModel scheduledJob);
    Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar);
    Task InsertEconomicCalendarsAsync(ICollection<EconomicCalendarReadModel> economicCalendars);
    Task InsertMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio);
    Task InsertMDIForwardLossRatiosAsync(ICollection<MDIForwardLossRatioReadModel> mdiForwardLossRatios);
    Task UpdateScheduledJobAsync(ScheduledJobReadModel scheduledJob);
    Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel e);
    Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel e);
    Task UpdateMDIForwardLossRatioAsync(MDIForwardLossRatioReadModel mdiForwardLossRatio);
}
