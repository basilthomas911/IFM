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

/// <summary>Handles the GetFuturesAdxDailySignalQuery query.</summary>
public static class GetFuturesAdxDailySignal
{
    /// <summary>Reads and replies to the GetFuturesAdxDailySignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesAdxDailySignalQuery q,
        IQueryActorContext<FuturesAdxSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (q as GetFuturesAdxDailySignalQuery)!;
        var queryResult = await query.GetLastFuturesAdxDailySignalAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesAdxSignalReadModel?>(queryResult);
        await ctx.ReplyAsync(q.Subject.ThreadId, GetFuturesAdxDailySignalQuery.Verb, serviceResult).ConfigureAwait(false);
    }
}
