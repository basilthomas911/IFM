using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

public static class GetFuturesItiSignal
{
    /// <summary>
    /// Handles <see cref="GetFuturesItiSignalQuery"/> by retrieving the latest ITI signal snapshot for the
    /// requested contract and value date, then replying to the caller.
    /// </summary>
    internal static async ValueTask GetLastFuturesItiSignalAsync(
        this GetFuturesItiSignalQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        var result = await dbFactory.MarketDataDb.GetLastFuturesItiSignalAsync(q.ContractId, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesItiSignalQuery.Verb, new ServiceResult<FuturesItiSignalV2ReadModel?>(result));
    }
}
