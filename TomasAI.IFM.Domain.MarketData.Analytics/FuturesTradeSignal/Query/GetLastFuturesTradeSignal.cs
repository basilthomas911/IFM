using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetLastFuturesTradeSignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask GetLastFuturesTradeSignalAsync(
        this GetLastFuturesTradeSignalQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        var result = await dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetLastFuturesTradeSignalQuery.Verb, new ServiceResult<FuturesTradeSignalV2ReadModel?>(result));
    }

}
