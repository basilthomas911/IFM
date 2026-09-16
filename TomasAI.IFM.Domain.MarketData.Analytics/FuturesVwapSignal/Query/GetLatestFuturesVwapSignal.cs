using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query;

/// <summary>Handles one latest-VWAP query and its typed reply.</summary>
public static class GetLatestFuturesVwapSignal
{
    /// <summary>Reads the current VWAP projection and replies to the caller.</summary>
    public static async ValueTask ExecuteAsync(
        this GetLatestFuturesVwapSignalQuery query,
        IQueryActorContext<FuturesVwapSignalQueryActor> context,
        IFuturesVwapSignalQueryContext typedContext,
        CancellationToken cancellationToken)
    {
        var current = await FuturesVwapSignalQueryModel.ExecuteAsync(query,
            typedContext.DbFactory, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<FuturesVwapSignalReadModel?>(current)).ConfigureAwait(false);
    }
}
