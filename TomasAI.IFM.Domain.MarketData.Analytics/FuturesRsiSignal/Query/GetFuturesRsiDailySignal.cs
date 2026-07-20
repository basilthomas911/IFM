using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query;

public static class GetFuturesRsiDailySignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask<FuturesRsiSignalReadModel?> GetLastFuturesRsiDailySignalAsync(
        this GetFuturesRsiDailySignalQuery q, IDbContextFactory dbFactory)
        =>  await dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(q.ContractId, q.TimePeriod, q.PeriodLength);
}
