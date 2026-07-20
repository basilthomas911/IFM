using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

public static class GetFuturesItiTrendDirectionChangedSignals
{
    /// <summary>
    /// Handles the GetFuturesItiTrendDirectionChangedSignalsQuery by retrieving the relevant Futures ITI trend direction changed signals from the database and replying with the results.
    /// </summary>
    /// <param name="q">The query for retrieving trend direction changed signals.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask GetFuturesItiTrendDirectionChangedSignalsAsync(
        this GetFuturesItiTrendDirectionChangedSignalsQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        FuturesItiSignalV2ReadModel[] result = [.. await dbFactory.MarketDataDb.GetFuturesItiTrendDirectionChangedSignalsAsync(q.ContractId, q.ValueDate)];
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesItiTrendDirectionChangedSignalsQuery.Verb, new ServiceResult<FuturesItiSignalV2ReadModel[]>(result));
    }
}
