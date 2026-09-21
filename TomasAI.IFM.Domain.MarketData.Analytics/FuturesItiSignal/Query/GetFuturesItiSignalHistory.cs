using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;

/// <summary>Handles complete historical Futures ITI timeframe reads.</summary>
public static class GetFuturesItiSignalHistory
{
    /// <summary>Returns all durable signals in chronological order for the requested timeframe.</summary>
    internal static async ValueTask<FuturesItiSignalV2ReadModel[]> GetFuturesItiSignalHistoryAsync(
        this GetFuturesItiSignalHistoryQuery query,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var window = FuturesItiSignalHistoryWindow.Resolve(query.ValueDate, query.TimePeriod);
        var startValueDate = query.TimePeriod == TimeFrameType.Daily
            ? window.StartValueDate.AddDays(-1)
            : window.StartValueDate;
        var rows = await dbFactory.MarketDataDb.GetFuturesItiSignalsAsync(
            query.Symbol,
            startValueDate,
            window.EndValueDate).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return [.. rows
            .Where(row => row.TimePeriod == query.TimePeriod)
            .OrderBy(static row => row.IntrinsicTime)
            .ThenBy(static row => row.SequenceId)];
    }

    /// <summary>Reads and replies to the GetFuturesItiSignalHistoryQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesItiSignalHistoryQuery q,
        IQueryActorContext<FuturesItiSignalQueryActor> ctx,
        IDbContextFactory db,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesItiSignalHistoryQuery)!;
        var result = await query.GetFuturesItiSignalHistoryAsync(db, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesItiSignalHistoryQuery.Verb,
            new ServiceResult<FuturesItiSignalV2ReadModel[]>(result)).ConfigureAwait(false);
    }
}
