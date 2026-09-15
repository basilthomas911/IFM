using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

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
        this GetFuturesRsiDailySignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
                q.ContractId, q.TimePeriod, q.PeriodLength, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesRsiDailySignalAsync(
                q.ContractId, q.TimePeriod, q.PeriodLength).ConfigureAwait(false);

    /// <summary>Reads and replies to the GetFuturesRsiDailySignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesRsiDailySignalQuery q,
        IQueryActorContext<FuturesRsiSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesRsiDailySignalQuery)!;
        var result = await query.GetLastFuturesRsiDailySignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesRsiSignalReadModel?>(result);
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesRsiDailySignalQuery.Verb, serviceResult).ConfigureAwait(false);
    }
}
