using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Query;

public static class GetFuturesEodDataMovingAverages
{
    internal static async ValueTask GetFuturesEodMovingAveragesAsync(
       this GetFuturesEodDataMovingAveragesQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.GetFuturesEodMovingAveragesAsync(q.ContractId, q.Symbol, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesEodDataQuery.Verb, new ServiceResult<FuturesEodDataMovingAveragesReadModel>(result));
    }
}
