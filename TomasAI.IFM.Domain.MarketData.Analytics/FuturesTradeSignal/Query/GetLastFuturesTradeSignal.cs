using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetLastFuturesTradeSignal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask<FuturesTradeSignalV2ReadModel?> GetLastFuturesTradeSignalAsync(
        this GetLastFuturesTradeSignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync(cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetLastFuturesTradeSignalAsync().ConfigureAwait(false);


    /// <summary>Reads and replies to the GetLastFuturesTradeSignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetLastFuturesTradeSignalQuery q,
        IQueryActorContext<FuturesTradeSignalQueryActor> ctx,
        IDbContextFactory db,
        CancellationToken cancellationToken)
    {
        var query = (q as GetLastFuturesTradeSignalQuery)!;
        var result = await query.GetLastFuturesTradeSignalAsync(db, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await ctx.ReplyAsync(q.Subject.ThreadId, GetLastFuturesTradeSignalQuery.Verb,
            new ServiceResult<FuturesTradeSignalV2ReadModel?>(result)).ConfigureAwait(false);
    }
}
