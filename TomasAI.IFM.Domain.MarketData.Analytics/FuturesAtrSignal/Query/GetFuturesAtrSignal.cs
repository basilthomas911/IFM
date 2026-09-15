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

public static class GetFuturesAtrSignal
{
    /// <summary>
    /// Handles a <see cref="GetFuturesAtrSignalQuery"/> by retrieving the most recent ATR signal
    /// for the specified futures contract and value date. The result is published back to the caller via a NATS reply.
    /// </summary>
    private static async ValueTask<FuturesAtrSignalReadModel?> GetLastFuturesAtrSignalAsync(
        this GetFuturesAtrSignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesAtrSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength).ConfigureAwait(false);


    /// <summary>Reads and replies to the GetFuturesAtrSignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesAtrSignalQuery q,
        IQueryActorContext<FuturesAtrSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesAtrSignalQuery)!;
        var queryResult = await query.GetLastFuturesAtrSignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesAtrSignalReadModel?>(queryResult);
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesAtrSignalQuery.Verb, serviceResult).ConfigureAwait(false);
    }
}
