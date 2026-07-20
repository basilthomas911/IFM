using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRateYears
{
    public static async ValueTask GetYieldCurveRateYearsAsync(this GetYieldCurveRateYearsQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var years = await dbFactory.MarketDataDb.GetYieldCurveRateYearsAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetYieldCurveRateYearsQuery.Verb, new ServiceResult<YieldCurveRateYearsReadModel>(new YieldCurveRateYearsReadModel([.. years])));
    }

}
