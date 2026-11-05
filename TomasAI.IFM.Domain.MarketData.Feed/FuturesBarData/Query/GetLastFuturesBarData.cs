using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;

public static class GetLastFuturesBarData
{

    /// <param name="q">
    ///
    /// </param>
    extension(GetLastFuturesBarDataQuery q)
    {
        /// <summary>Returns the typed query result from the projected read store.</summary>
        internal async ValueTask<ServiceResult<FuturesBarDataReadModel>> ReadAsync(IDbContextFactory databases)
            => new(await q.ExecuteAsync(databases).ConfigureAwait(false));

        /// <summary>
        ///
        /// </summary>
        ///
        /// <param name="context"></param>
        /// <param name="dbFactory"></param>
        /// <returns></returns>
        internal async ValueTask<FuturesBarDataReadModel> ExecuteAsync(
        IDbContextFactory dbFactory)
            => await (dbFactory.MarketDataDb.GetLastFuturesBarDataAsync(q.ContractId, q.Symbol, q.ValueDate)).ConfigureAwait(false);
    }
}
