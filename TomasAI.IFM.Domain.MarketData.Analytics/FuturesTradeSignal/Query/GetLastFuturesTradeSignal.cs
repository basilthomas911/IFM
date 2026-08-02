using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetLastFuturesTradeSignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(
        this GetLastFuturesTradeSignalQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync();

}
