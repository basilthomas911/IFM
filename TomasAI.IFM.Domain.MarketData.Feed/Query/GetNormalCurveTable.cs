using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetNormalCurveTable
{
    internal static async ValueTask GetNormalCurveTableAsync(
        this GetNormalCurveTableQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.MarketDataDb.GetNormalCurveTableAsync();
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractQuery.Verb, new ServiceResult<NormalCurveTableReadModel>(result));
    }
}
