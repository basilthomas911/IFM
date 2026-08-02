using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetFuturesTradeSignalIds
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask<FuturesTradeSignalId[]> GetFuturesTradeSignalIdsAsync(
        this GetFuturesTradeSignalIdsQuery q, IDbContextFactory dbFactory)
        => [.. await dbFactory.MarketDataDb.GetFuturesTradeSignalIdByValueDateAsync(q.ValueDate)];
}
