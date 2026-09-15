using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Query;

public static class GetFuturesAtrDailySignal
{
    /// <summary>
    /// Handles a <see cref="GetFuturesAtrSignalQuery"/> by retrieving the most recent ATR signal
    /// for the specified futures contract and value date. The result is published back to the caller via a NATS reply.
    /// </summary>
    private static async ValueTask<FuturesAtrSignalReadModel?> GetLastFuturesAtrDailySignalAsync(
        this GetFuturesAtrDailySignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesAtrDailySignalAsync(
                q.ContractId, q.TimePeriod, q.PeriodLength, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesAtrDailySignalAsync(
                q.ContractId, q.TimePeriod, q.PeriodLength).ConfigureAwait(false);


    /// <summary>Reads and replies to the GetFuturesAtrDailySignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesAtrDailySignalQuery q,
        IQueryActorContext<FuturesAtrSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesAtrDailySignalQuery)!;
        var queryResult = await query.GetLastFuturesAtrDailySignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesAtrSignalReadModel?>(queryResult);
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesAtrDailySignalQuery.Verb, serviceResult).ConfigureAwait(false);
    }
}
