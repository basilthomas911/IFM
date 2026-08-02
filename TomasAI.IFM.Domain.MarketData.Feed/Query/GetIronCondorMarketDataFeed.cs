using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetIronCondorMarketDataFeed
{
    public static async ValueTask<IronCondorMarketDataFeedReadModel> GetIronCondorMarketDataFeedAsync(
        this GetIronCondorMarketDataFeedQuery q, IDbContextFactory dbFactory)
    {
        var db = dbFactory.MarketDataDb;
        return await GetIronCondorMarketDataFeedAsync(db,
            q.UnderlyingContractId,
            q.ShortPutOptionContractId,
            q.LongPutOptionContractId,
            q.ShortCallOptionContractId,
            q.LongCallOptionContractId,
            q.ValueDate);

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
