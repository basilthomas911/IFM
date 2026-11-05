using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;

public static class GetNormalCurveTable
{

    extension(GetNormalCurveTableQuery q)
    {
        internal async ValueTask<NormalCurveTableReadModel> ExecuteAsync(
        IDbContextFactory dbFactory)
            => await (dbFactory.MarketDataDb.GetNormalCurveTableAsync()).ConfigureAwait(false);
    }

    extension(GetNormalCurveTableQuery query)
    {
        /// <summary>Returns the typed market-data feed result.</summary>
        public async ValueTask<ServiceResult<NormalCurveTableReadModel>> ExecuteAsync(
            TomasAI.IFM.Domain.MarketData.Feed.Query.Actor.IMarketDataFeedQueryContext context,
            MarketDataFeedQueryParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(query);
            var result = await query.ExecuteAsync(parameters.DbFactory).ConfigureAwait(false);
            return new TomasAI.IFM.Shared.EventSourcing.ServiceResult<NormalCurveTableReadModel>(result);
        }
    }
}
