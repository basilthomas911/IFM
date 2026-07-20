using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Reference.ServiceApi;

public interface IReferenceCommandApi
{
    Task<ServiceResult<Guid>> AddEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar);
    Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(EconomicCalendarId economicCalendarId, bool overwrite);
    Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar, bool overwrite);
    Task<ServiceResult<Guid>> ImportEconomicCalendarsAsync(DateTime importedDate, EconomicCalendarReadModel[] economicCalendars);
    Task<ServiceResult<Guid>> AddLookupTypeAsync(LookupTypeReadModel lookupType);
    Task<ServiceResult<Guid>> RemoveLookupTypeAsync(LookupTypeId lookupTypeId, bool overwrite);
    Task<ServiceResult<Guid>> ChangeLookupTypeAsync(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, bool overwrite);
}
