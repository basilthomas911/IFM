using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query;

public static class GetFuturesMacdSignal
{
    /// <summary>
    /// Handles the GetFuturesMacdSignalQuery by retrieving the last Futures MACD signal for a given contract and value date, and replies with the result.
    /// </summary>
    /// <param name="q">The query for retrieving the Futures MACD signal.</param>
    /// <param name="dbFactory">The database context factory.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    internal static async ValueTask<FuturesMacdSignalReadModel?> GetLastFuturesMacdSignalAsync(
        this GetFuturesMacdSignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
                q.ContractId,
                q.ValueDate,
                q.TimePeriod,
                q.SignalEmaPeriod,
                q.FastEmaPeriod,
                q.SlowEmaPeriod,
                cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesMacdSignalAsync(
                q.ContractId,
                q.ValueDate,
                q.TimePeriod,
                q.SignalEmaPeriod,
                q.FastEmaPeriod,
                q.SlowEmaPeriod).ConfigureAwait(false);

    /// <summary>Reads and replies to the GetFuturesMacdSignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesMacdSignalQuery q,
        IQueryActorContext<FuturesMacdSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesMacdSignalQuery)!;
        var result = await query.GetLastFuturesMacdSignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesMacdSignalReadModel>(result);
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesMacdSignalQuery.Verb, serviceResult).ConfigureAwait(false);
    }
}
