using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query;

public static class GetFuturesAtrSignal
{
    /// <summary>
    /// Handles a <see cref="GetFuturesAtrSignalQuery"/> by retrieving the most recent ATR signal
    /// for the specified futures contract and value date. The result is published back to the caller via a NATS reply.
    /// </summary>
    public static async ValueTask<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(this GetFuturesAtrSignalQuery q, IDbContextFactory dbFactory)
        => await dbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength);

}
