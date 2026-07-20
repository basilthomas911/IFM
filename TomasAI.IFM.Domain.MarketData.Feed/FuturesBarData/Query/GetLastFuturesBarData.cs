using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

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
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesBarDataQuery.Verb, new ServiceResult<FuturesBarDataReadModel>(result));
    }
}
