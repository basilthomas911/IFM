using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query;

public static class GetFuturesAtrDailySignal
{
    /// <summary>
    /// Handles a <see cref="GetFuturesAtrSignalQuery"/> by retrieving the most recent ATR signal
    /// for the specified futures contract and value date. The result is published back to the caller via a NATS reply.
    /// </summary>
    public static async ValueTask<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(this GetFuturesAtrDailySignalQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetLastFuturesAtrDailySignalAsync(q.ContractId,  q.TimePeriod, q.PeriodLength);

}
