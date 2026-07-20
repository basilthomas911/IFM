using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;

public static class GetFuturesBarData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    internal static async ValueTask GetFuturesBarDataAsync(
       this GetFuturesBarDataQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.MarketDataDb.GetFuturesBarDataAsync(q.ContractId, q.Symbol, q.ValueDate, q.StartDate, q.EndDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesBarDataQuery.Verb, new ServiceResult<FuturesBarDataReadModel[]>([.. result]));
    }
}
