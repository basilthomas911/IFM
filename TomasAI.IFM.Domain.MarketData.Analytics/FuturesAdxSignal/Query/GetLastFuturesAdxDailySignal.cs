using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query;

public static class GetLastFuturesAdxDailySignal
{
    /// <summary>
    /// Handles a <see cref="GetFuturesAdxDailySignalQuery"/> by retrieving the most recent ADX signal
    /// for the specified futures contract and value date. The result is published back to the caller via a NATS reply.
    /// </summary>
    public static async ValueTask<FuturesAdxSignalReadModel?> GetLastFuturesAdxDailySignalAsync(this GetFuturesAdxDailySignalQuery q, IDbContextFactory dbFactory)
        =>  await dbFactory.MarketDataDb.GetLastFuturesAdxDailySignalAsync(q.ContractId,  q.TimePeriod, q.PeriodLength);
}
