using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Query.Actor;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

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
    internal static async ValueTask<FuturesTdiSignalReadModel?> GetFuturesTdiSignalAsync(
        this GetFuturesTdiSignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => await dbFactory.MarketDataDb.GetLastFuturesTdiSignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.ConfigurationId, cancellationToken).ConfigureAwait(false);

    /// <summary>Reads and replies to the GetFuturesTdiSignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesTdiSignalQuery q,
        IQueryActorContext<FuturesTdiSignalQueryActor> ctx,
        IDbContextFactory db,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(q);
        var query = q;
        var result = await query.GetFuturesTdiSignalAsync(db, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesTdiSignalQuery.Verb,
            new ServiceResult<FuturesTdiSignalReadModel?>(result)).ConfigureAwait(false);
    }
}
