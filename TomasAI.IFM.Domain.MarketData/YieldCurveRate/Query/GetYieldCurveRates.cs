using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRates
{
    public static async ValueTask GetYieldCurveRatesAsync(this GetYieldCurveRatesQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var yieldCurveRates = await dbFactory.MarketDataDb.GetYieldCurveRatesAsync(q.StartDate, q.EndDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetYieldCurveRatesQuery.Verb, new ServiceResult<YieldCurveRateReadModel[]>([.. yieldCurveRates]));
    }
}
