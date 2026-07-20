using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Query;

internal static class GetLastYieldCurveRate
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask GetLastYieldCurveRateAsync(this GetLastYieldCurveRateQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var yieldCurveRate = await dbFactory.MarketDataDb.GetLastYieldCurveRateAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractQuery.Verb, new ServiceResult<YieldCurveRateReadModel?>(yieldCurveRate));
    }
}
