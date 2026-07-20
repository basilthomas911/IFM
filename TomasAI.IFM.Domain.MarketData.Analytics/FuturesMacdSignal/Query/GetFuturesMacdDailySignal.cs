using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query;

public static class GetFuturesMacdDailySignal
{
    /// <summary>
    /// Handles the GetFuturesMacdDailySignalQuery by retrieving the last Futures MACD signal for a given contract, time period and period length, and replies with the result.
    /// </summary>
    /// <param name="q">The query for retrieving the Futures MACD daily signal.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FuturesMacdSignalReadModel?> GetLastFuturesMacdDailySignalAsync(
        this GetFuturesMacdDailySignalQuery q, IDbContextFactory dbFactory)
        =>  await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(q.ContractId, q.TimePeriod, q.PeriodLength);
    
}
