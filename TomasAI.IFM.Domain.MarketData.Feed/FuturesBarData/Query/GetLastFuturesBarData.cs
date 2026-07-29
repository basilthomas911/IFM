using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;

public static class GetLastFuturesBarData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    internal static async ValueTask GetLastFuturesBarDataAsync(
       this GetLastFuturesBarDataQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        FuturesBarDataReadModel result = await dbFactory.MarketDataDb.GetLastFuturesBarDataAsync(q.ContractId, q.Symbol, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetLastFuturesBarDataQuery.Verb, new ServiceResult<FuturesBarDataReadModel>(result));
    }
}
