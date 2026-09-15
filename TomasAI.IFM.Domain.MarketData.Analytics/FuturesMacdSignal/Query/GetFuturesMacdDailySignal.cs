using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

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
        this GetFuturesMacdDailySignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
                q.ContractId,
                q.TimePeriod,
                q.SignalEmaPeriod,
                q.FastEmaPeriod,
                q.SlowEmaPeriod,
                cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesMacdDailySignalAsync(
                q.ContractId,
                q.TimePeriod,
                q.SignalEmaPeriod,
                q.FastEmaPeriod,
                q.SlowEmaPeriod).ConfigureAwait(false);
    

    /// <summary>Reads and replies to the GetFuturesMacdDailySignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesMacdDailySignalQuery q,
        IQueryActorContext<FuturesMacdSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesMacdDailySignalQuery)!;
        var result = await query.GetLastFuturesMacdDailySignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesMacdSignalReadModel>(result);
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesMacdDailySignalQuery.Verb, serviceResult).ConfigureAwait(false);
    }
}
