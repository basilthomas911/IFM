using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query;

public static class GetLastFuturesAdxSignal
{
    /// <summary>
    /// Handles a <see cref="GetFuturesAdxSignalQuery"/> by retrieving the most recent ADX signal
    /// for the specified futures contract and value date. The result is published back to the caller via a NATS reply.
    /// </summary>
    public static async ValueTask<FuturesAdxSignalReadModel?> GetLastFuturesAdxSignalAsync(this GetFuturesAdxSignalQuery q, IDbContextFactory dbFactory)
        =>  await dbFactory.MarketDataDb.GetLastFuturesAdxSignalAsync(q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength);
}
