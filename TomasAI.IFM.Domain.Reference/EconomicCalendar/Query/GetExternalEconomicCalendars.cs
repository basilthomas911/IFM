using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

public static class GetExternalEconomicCalendars
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask<EconomicCalendarReadModel[]> GetExternalEconomicCalendarsAsync(
        this GetExternalEconomicCalendarsQuery q, IDbContextFactory dbFactory)
        => await dbFactory.GetExternalEconomicCalendarsAsync();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    static async ValueTask<EconomicCalendarReadModel[]> GetExternalEconomicCalendarsAsync(this IDbContextFactory dbFactory)
    {
        if (dbFactory.EconomicCalendarsDb is not IEconomicCalendarsDbContext ecCal)
            return [];
        return [.. await ecCal.ReadAsync()];
    }
}
