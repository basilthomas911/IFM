using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetIronCondorMarketDataFeed
{

    extension(GetIronCondorMarketDataFeedQuery q)
    {
        public async ValueTask<IronCondorMarketDataFeedReadModel> ExecuteAsync(
        IDbContextFactory dbFactory)
        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            RawTickRequestQualification.RequireScope(q.UnderlyingContractId, q.ValueDate);
            RawTickRequestQualification.RequireScope(q.ShortPutOptionContractId, q.ValueDate);
            RawTickRequestQualification.RequireScope(q.LongPutOptionContractId, q.ValueDate);
            RawTickRequestQualification.RequireScope(q.ShortCallOptionContractId, q.ValueDate);
            RawTickRequestQualification.RequireScope(q.LongCallOptionContractId, q.ValueDate);
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
    }

    extension(GetIronCondorMarketDataFeedQuery query)
    {
        /// <summary>Returns the typed market-data feed result.</summary>
        public async ValueTask<ServiceResult<IronCondorMarketDataFeedReadModel>> ExecuteAsync(
            TomasAI.IFM.Domain.MarketData.Feed.Query.Actor.IMarketDataFeedQueryContext context,
            MarketDataFeedQueryParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(query);
            var result = await query.ExecuteAsync(parameters.DbFactory).ConfigureAwait(false);
            return new TomasAI.IFM.Shared.EventSourcing.ServiceResult<IronCondorMarketDataFeedReadModel>(result);
        }
    }
}
