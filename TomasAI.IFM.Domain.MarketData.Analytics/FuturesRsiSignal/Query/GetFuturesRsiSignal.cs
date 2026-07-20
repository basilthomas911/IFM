using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query;

public static class GetFuturesRsiSignal
{
    /// <summary>
    /// Handles a request to retrieve the most recent RSI signal for a specified contract, value date, and signal type.
    /// </summary>
    /// <param name="q">The query containing contract identifier, value date, and signal type filters.</param>
    /// <param name="dbFactory">The database context factory used to access futures RSI signal data.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    public static async ValueTask<FuturesRsiSignalReadModel?>  GetLastFuturesRsiSignalAsync(this GetFuturesRsiSignalQuery q, IDbContextFactory dbFactory)
        =>  await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(q.ContractId, q.ValueDate,  q.TimePeriod, q.PeriodLength);
}
