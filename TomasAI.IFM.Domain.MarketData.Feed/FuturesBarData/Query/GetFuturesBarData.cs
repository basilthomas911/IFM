using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;

public static class GetFuturesBarData
{

    /// <param name="q">
    ///
    /// </param>
    extension(GetFuturesBarDataQuery q)
    {
        /// <summary>Returns the typed query result from the projected read store.</summary>
        internal async ValueTask<ServiceResult<FuturesBarDataReadModel[]>> ReadAsync(IDbContextFactory databases)
            => new(await q.ExecuteAsync(databases).ConfigureAwait(false));

        /// <summary>
        ///
        /// </summary>
        ///
        /// <param name="context"></param>
        /// <param name="dbFactory"></param>
        /// <returns></returns>
        internal async ValueTask<FuturesBarDataReadModel[]> ExecuteAsync(
        IDbContextFactory dbFactory)
        {
            if (!q.AcrossValueDates)
                return [.. await dbFactory.MarketDataDb.GetFuturesBarDataAsync(q.ContractId, q.Symbol, q.ValueDate, q.StartDate, q.EndDate).ConfigureAwait(false)];
            var dates = FuturesBarWindowPartitions.Get(q.ValueDate, q.StartDate, q.EndDate);
            var bars = new List<FuturesBarDataReadModel>();
            foreach (var date in dates)
                bars.AddRange(await dbFactory.MarketDataDb.GetFuturesBarDataAsync(q.ContractId, q.Symbol, date, q.StartDate, q.EndDate).ConfigureAwait(false));
            return [.. bars.Where(bar => bar.BarDate >= q.StartDate && bar.BarDate <= q.EndDate)
                .DistinctBy(bar => (bar.ContractId, bar.Symbol, bar.BarDate, bar.BarRateType)).OrderBy(bar => bar.BarDate)];
        }
    }
}
