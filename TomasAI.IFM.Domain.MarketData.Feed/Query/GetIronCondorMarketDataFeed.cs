using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetIronCondorMarketDataFeed
{
    public static async ValueTask GetIronCondorMarketDataFeedAsync(
        this GetIronCondorMarketDataFeedQuery q,  IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var db = dbFactory.MarketDataDb;
        var result = await GetIronCondorMarketDataFeedAsync(db,
            q.UnderlyingContractId,
            q.ShortPutOptionContractId,
            q.LongPutOptionContractId,
            q.ShortCallOptionContractId,
            q.LongCallOptionContractId,
            q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetIronCondorMarketDataFeedQuery.Verb, new ServiceResult<IronCondorMarketDataFeedReadModel>(result));

        async ValueTask<IronCondorMarketDataFeedReadModel> GetIronCondorMarketDataFeedAsync(
            IMarketDataDbContext db,
            string underlyingContractId,
            string shortPutOptionContractId,
            string longPutOptionContractId,
            string shortCallOptionContractId,
            string longCallOptionContractId,
            DateOnly valueDate)
            => new(
                assetPrice: Convert.ToDecimal((await db.GetLastFuturesTickDataAsync(underlyingContractId, valueDate))?.Price ?? 0),
                shortPutOptionData: (await db.GetLastFuturesOptionTickDataAsync(shortPutOptionContractId, valueDate))!,
                longPutOptionData: (await db.GetLastFuturesOptionTickDataAsync(longPutOptionContractId, valueDate))!,
                shortCallOptionData: (await db.GetLastFuturesOptionTickDataAsync(shortCallOptionContractId, valueDate))!,
                longCallOptionData: (await db.GetLastFuturesOptionTickDataAsync(longCallOptionContractId, valueDate))!);
    }
}
