using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Query;

public static class GetFuturesBarData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    internal static async ValueTask<FuturesBarDataReadModel[]> ExecuteAsync(
       this GetFuturesBarDataQuery q, IDbContextFactory dbFactory)
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
