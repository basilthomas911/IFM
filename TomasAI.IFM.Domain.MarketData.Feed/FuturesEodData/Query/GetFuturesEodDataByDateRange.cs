using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodDataByDateRange
{
    internal static async ValueTask GetFuturesEodDataByDateRangeAsync(
       this GetFuturesEodDataByDateRangeQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.MarketDataDb.GetFuturesEodDataByDateRangeAsync(q.ContractId, q.StartDate, q.EndDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataByDateRangeQuery.Verb, new ServiceResult<FuturesEodDataV2ReadModel[]>([.. result]));
    }
}
