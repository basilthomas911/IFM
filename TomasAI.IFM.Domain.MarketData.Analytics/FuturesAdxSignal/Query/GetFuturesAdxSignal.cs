using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Query;

/// <summary>Handles the GetFuturesAdxSignalQuery query.</summary>
public static class GetFuturesAdxSignal
{
    /// <summary>Reads and replies to the GetFuturesAdxSignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesAdxSignalQuery q,
        IQueryActorContext<FuturesAdxSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesAdxSignalQuery)!;
        var queryResult = await query.GetLastFuturesAdxSignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesAdxSignalReadModel?>(queryResult);
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesAdxSignalQuery.Verb, serviceResult).ConfigureAwait(false);
    }
}
