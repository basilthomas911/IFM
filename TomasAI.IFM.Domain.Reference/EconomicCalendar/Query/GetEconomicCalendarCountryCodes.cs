using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Reference.Queries;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

public static class GetEconomicCalendarCountryCodes
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask GetEconomicCalendarCountryCodesAsync(this GetEconomicCalendarCountryCodesQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var countryCodes = await dbFactory.ReferenceDb.GetEconomicCalendarCountryCodesAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetEconomicCalendarQuery.Verb, new ServiceResult<EconomicCalendarCountryCodeReadModel[]>([.. countryCodes]));
    }
}
