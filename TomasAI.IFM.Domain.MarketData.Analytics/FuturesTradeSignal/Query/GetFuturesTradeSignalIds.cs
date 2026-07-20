using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetFuturesTradeSignalIds
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask GetFuturesTradeSignalIdsAsync(
        this GetFuturesTradeSignalIdsQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        FuturesTradeSignalId[] result = [.. await dbFactory.MarketDataDb.GetFuturesTradeSignalIdByValueDateAsync(q.ValueDate)];
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesTradeSignalIdsQuery.Verb, new ServiceResult<FuturesTradeSignalId[]>(result));
    }
}
