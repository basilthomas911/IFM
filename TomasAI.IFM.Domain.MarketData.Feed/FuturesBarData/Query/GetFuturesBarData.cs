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
    internal static async ValueTask<FuturesBarDataReadModel[]> GetFuturesBarDataAsync(
       this GetFuturesBarDataQuery q, IDbContextFactory dbFactory)
        => [.. await dbFactory.MarketDataDb.GetFuturesBarDataAsync(q.ContractId, q.Symbol, q.ValueDate, q.StartDate, q.EndDate)];
}
