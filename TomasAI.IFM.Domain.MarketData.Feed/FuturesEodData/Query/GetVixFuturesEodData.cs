using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetVixFuturesEodData
{
    internal static async ValueTask GetVixFuturesEodDataAsync(
       this GetVixFuturesEodDataQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var db = dbFactory.MarketDataDb;
        var result = string.IsNullOrEmpty(q.ContractId)
            ? await db.GetVixFuturesEodDataByValueDateAsync(q.ValueDate)
            : [await db.GetVixFuturesEodDataAsync(q.ContractId, q.ValueDate)];
        await context.ReplyAsync(q.Subject.ThreadId, GetVixFuturesEodDataQuery.Verb, new ServiceResult<VixFuturesEodDataReadModel[]>([.. result]));
    }
}
