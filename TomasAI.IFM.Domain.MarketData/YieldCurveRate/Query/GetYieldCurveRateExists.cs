using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

public static class GetYieldCurveRateExists
{
    public static async ValueTask GetYieldCurveRateExistsAsync(this GetYieldCurveRateExistsQuery q,IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var exists = await dbFactory.MarketDataDb.GetYieldCurveRateExistsAsync(q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetYieldCurveRateExistsQuery.Verb, new ServiceResult<ScalarReadModel<bool>>(new ScalarReadModel<bool>(exists)));
    }
}
