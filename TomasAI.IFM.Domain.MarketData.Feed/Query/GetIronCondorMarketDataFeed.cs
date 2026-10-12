using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetIronCondorMarketDataFeed
{
    public static async ValueTask<IronCondorMarketDataFeedReadModel> ExecuteAsync(
        this GetIronCondorMarketDataFeedQuery q, IDbContextFactory dbFactory)
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        ArgumentNullException.ThrowIfNull(q);
        var db = dbFactory.MarketDataDb;
        return await (GetIronCondorMarketDataFeedAsync(db,
            q.UnderlyingContractId,
            q.ShortPutOptionContractId,
            q.LongPutOptionContractId,
            q.ShortCallOptionContractId,
            q.LongCallOptionContractId,
            q.ValueDate)).ConfigureAwait(false);

        async ValueTask<IronCondorMarketDataFeedReadModel> GetIronCondorMarketDataFeedAsync(
            IMarketDataDbContext db,
            string underlyingContractId,
            string shortPutOptionContractId,
            string longPutOptionContractId,
            string shortCallOptionContractId,
            string longCallOptionContractId,
            DateOnly valueDate)
            => new(
                assetPrice: Convert.ToDecimal((await (db.GetLastFuturesTickDataAsync(underlyingContractId, valueDate)).ConfigureAwait(false))?.Price ?? 0),
                shortPutOptionData: (await (db.GetLastFuturesOptionTickDataAsync(shortPutOptionContractId, valueDate)).ConfigureAwait(false))!,
                longPutOptionData: (await (db.GetLastFuturesOptionTickDataAsync(longPutOptionContractId, valueDate)).ConfigureAwait(false))!,
                shortCallOptionData: (await (db.GetLastFuturesOptionTickDataAsync(shortCallOptionContractId, valueDate)).ConfigureAwait(false))!,
                longCallOptionData: (await (db.GetLastFuturesOptionTickDataAsync(longCallOptionContractId, valueDate)).ConfigureAwait(false))!);
    }

    /// <summary>Reads and replies with the requested market-data feed result.</summary>
    public static async ValueTask ExecuteAsync(
        this GetIronCondorMarketDataFeedQuery query,
        TomasAI.IFM.Domain.MarketData.Feed.Query.Actor.IMarketDataFeedQueryContext context,
        MarketDataFeedQueryParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(query);
        var result = await query.ExecuteAsync(parameters.DbFactory).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new TomasAI.IFM.Shared.EventSourcing.ServiceResult<IronCondorMarketDataFeedReadModel>(result)).ConfigureAwait(false);
    }
}
