using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query;

public static class GetLastFuturesTickData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask GetLastFuturesTickDataAsync(
        this GetLastFuturesTickDataQuery q,  IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.MarketDataDb.GetLastFuturesTickDataAsync(q.ContractId, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetLastFuturesTickDataQuery.Verb, new ServiceResult<FuturesTickDataV2ReadModel?>(result));
    }
}
