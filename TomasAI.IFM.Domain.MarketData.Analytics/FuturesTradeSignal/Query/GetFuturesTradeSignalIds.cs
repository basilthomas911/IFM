using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;

public static class GetFuturesTradeSignalIds
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="dbFactory"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    internal static async ValueTask<FuturesTradeSignalId[]> GetFuturesTradeSignalIdsAsync(
        this GetFuturesTradeSignalIdsQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => [.. cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb
                .GetFuturesTradeSignalIdByValueDateAsync(q.ValueDate, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb
                .GetFuturesTradeSignalIdByValueDateAsync(q.ValueDate).ConfigureAwait(false)];

    /// <summary>Reads and replies to the GetFuturesTradeSignalIdsQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesTradeSignalIdsQuery q,
        IQueryActorContext<FuturesTradeSignalQueryActor> ctx,
        IDbContextFactory db,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesTradeSignalIdsQuery)!;
        var result = await query.GetFuturesTradeSignalIdsAsync(db, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesTradeSignalIdsQuery.Verb,
            new ServiceResult<FuturesTradeSignalId[]>(result)).ConfigureAwait(false);
    }
}
