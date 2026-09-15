using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query;

/// <summary>Handles one VWAP history query and its typed reply.</summary>
public static class GetFuturesVwapSignalHistory
{
    /// <summary>Reads the requested history window and replies to the caller.</summary>
    public static async ValueTask ExecuteAsync(
        this GetFuturesVwapSignalHistoryQuery query,
        IQueryActorContext<FuturesVwapSignalQueryActor> context,
        IFuturesVwapSignalQueryContext typedContext,
        CancellationToken cancellationToken)
    {
        var values = await query.ExecuteAsync(
            typedContext.DbFactory, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<FuturesVwapSignalReadModel[]>(values)).ConfigureAwait(false);
    }
}
