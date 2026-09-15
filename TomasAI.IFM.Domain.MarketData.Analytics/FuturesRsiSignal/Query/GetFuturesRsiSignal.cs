using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query;

public static class GetFuturesRsiSignal
{
    /// <summary>
    /// Handles a request to retrieve the most recent RSI signal for a specified contract, value date, and signal type.
    /// </summary>
    /// <param name="q">The query containing contract identifier, value date, and signal type filters.</param>
    /// <param name="dbFactory">The database context factory used to access futures RSI signal data.</param>
    /// <returns>A <see cref="ValueTask"/> that completes after the reply has been sent.</returns>
    private static async ValueTask<FuturesRsiSignalReadModel?> GetLastFuturesRsiSignalAsync(
        this GetFuturesRsiSignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesRsiSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength).ConfigureAwait(false);

    /// <summary>Reads and replies to the GetFuturesRsiSignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesRsiSignalQuery q,
        IQueryActorContext<FuturesRsiSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesRsiSignalQuery)!;
        var result = await query.GetLastFuturesRsiSignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesRsiSignalReadModel?>(result);
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesRsiSignalQuery.Verb, serviceResult).ConfigureAwait(false);
    }
}
