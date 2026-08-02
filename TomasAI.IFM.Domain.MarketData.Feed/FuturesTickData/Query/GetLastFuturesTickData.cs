using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Query;

public static class GetLastFuturesTickData
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask<FuturesTickDataV2ReadModel?> GetLastFuturesTickDataAsync(
        this GetLastFuturesTickDataQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetLastFuturesTickDataAsync(q.ContractId, q.ValueDate);
}
