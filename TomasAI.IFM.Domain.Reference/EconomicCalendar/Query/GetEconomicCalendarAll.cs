using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

public static class GetEconomicCalendarAll
{
    /// <summary>
    /// Handles the GetEconomicCalendarAllQuery by retrieving all economic calendar entries from the database and replying with the results.
    /// </summary>
    /// <param name="q">The query requesting all economic calendar entries.</param>
    /// <param name="context">The query actor context for replying with results.</param>
    /// <param name="dbFactory">The database context factory used to access reference storage.</param>
    /// <returns>A value task that completes after the reply has been posted.</returns>
    public static async ValueTask GetEconomicCalendarAllAsync(this GetEconomicCalendarAllQuery q,IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var economicCalendars = await dbFactory.ReferenceDb.GetEconomicCalendarAllAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetEconomicCalendarAllQuery.Verb, new ServiceResult<EconomicCalendarReadModel[]>([.. economicCalendars]));
    }
}
