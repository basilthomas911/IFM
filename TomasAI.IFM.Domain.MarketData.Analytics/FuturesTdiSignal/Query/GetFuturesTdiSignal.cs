using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.Queries;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Query;

public static class GetFuturesTdiSignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask GetFuturesTdiSignalAsync(this GetFuturesTdiSignalQuery q, IDbContextFactory dbFactory, IQueryActorContext context)
    {
        var result = await dbFactory.MarketDataDb.GetLastFuturesTdiSignalAsync(q.ContractId, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesTdiSignalQuery.Verb, new ServiceResult<FuturesTdiSignalReadModel?>(result));
    }
}
