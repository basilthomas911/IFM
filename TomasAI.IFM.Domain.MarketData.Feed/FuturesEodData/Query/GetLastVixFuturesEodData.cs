using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetLastVixFuturesEodData
{
    internal static async ValueTask GetLastVixFuturesEodDataAsync(
       this GetLastVixFuturesEodDataQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.MarketDataDb.GetLastVixFuturesEodDataAsync(q.ContractId, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataQuery.Verb, new ServiceResult<VixFuturesEodDataReadModel?>(result));
    }
}
