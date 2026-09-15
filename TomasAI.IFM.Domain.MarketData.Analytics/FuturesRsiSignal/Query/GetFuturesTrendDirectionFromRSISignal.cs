using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query.Actor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Query;

/// <summary>Handles the GetFuturesTrendDirectionFromRSISignalQuery query.</summary>
public static class GetFuturesTrendDirectionFromRSISignal
{
    /// <summary>Loads the RSI trend-direction read model for this query.</summary>
    private static async ValueTask<FuturesTrendDirectionReadModel> GetFuturesTrendDirectionAsync(
        this GetFuturesTrendDirectionFromRSISignalQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => cancellationToken.CanBeCanceled
            ? await dbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength, q.Timestamp,
                q.LookBackInterval, q.StartTime, q.EndTime, cancellationToken).ConfigureAwait(false)
            : await dbFactory.MarketDataDb.GetFuturesTrendDirectionFromRSISignalAsync(
                q.ContractId, q.ValueDate, q.TimePeriod, q.PeriodLength, q.Timestamp,
                q.LookBackInterval, q.StartTime, q.EndTime).ConfigureAwait(false);

    /// <summary>Reads and replies to the GetFuturesTrendDirectionFromRSISignalQuery message.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesTrendDirectionFromRSISignalQuery q,
        IQueryActorContext<FuturesRsiSignalQueryActor> ctx,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        var query = (GetFuturesTrendDirectionFromRSISignalQuery)q;
        var result = await query.GetFuturesTrendDirectionAsync(dbFactory, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var serviceResult = new ServiceResult<FuturesTrendDirectionReadModel>(result);
        await ctx.ReplyAsync(
            q.Subject.ThreadId,
            GetFuturesTrendDirectionFromRSISignalQuery.Verb,
            serviceResult).ConfigureAwait(false);
    }
}
