using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Query;

public static class GetLastFuturesOptionTickData
{
    internal static async ValueTask GetLastFuturesOptionTickDataAsync(
       this GetLastFuturesOptionTickDataQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.MarketDataDb.GetLastFuturesOptionTickDataAsync(q.ContractId, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetLastFuturesOptionTickDataQuery.Verb, new ServiceResult<FuturesOptionTickDataV2ReadModel?>(result));
    }
}
