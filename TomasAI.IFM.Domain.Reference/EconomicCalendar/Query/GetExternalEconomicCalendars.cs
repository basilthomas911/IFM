using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;

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
    public static async ValueTask GetExternalEconomicCalendarsAsync(this GetExternalEconomicCalendarsQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var externalCalendars = await dbFactory.GetExternalEconomicCalendarsAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetExternalEconomicCalendarsQuery.Verb, new ServiceResult<EconomicCalendarReadModel[]>(externalCalendars));
    }

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
