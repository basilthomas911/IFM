using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetFuturesTradeSignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask GetFuturesTradeSignalAsync(
        this GetFuturesTradeSignalQuery q,IDbContextFactory dbFactory, IQueryActorContext context)
    {
        var result = await dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync(q.ContractId, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesTradeSignalQuery.Verb, new ServiceResult<FuturesTradeSignalV2ReadModel?>(result));
    }
}
