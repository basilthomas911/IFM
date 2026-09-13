using TomasAI.IFM.Domain.Trade.Futures.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Query;

public static class GetFuturesTrade
{
    /// <summary>
    /// Reads one established trade and returns it only when it belongs to the futures asset family.
    /// </summary>
    /// <param name="query">The query containing the globally unique trade identity.</param>
    /// <param name="context">The typed query context that provides storage and reply operations.</param>
    /// <param name="cancellationToken">The token that cancels the storage operation.</param>
    /// <returns>A task that completes after the typed query reply is sent.</returns>
    public static async ValueTask ExecuteAsync(
        this GetFuturesTradeQuery query,
        IFuturesTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var trade = await context.DbFactory.TradeDb
            .GetEstablishedTradeAsync(query.TradeId, cancellationToken)
            .ConfigureAwait(false);

        if (trade?.AssetFamily != TradeAssetFamily.Futures)
            trade = null;

        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<EstablishedTradeDefinition?>(trade)).ConfigureAwait(false);
    }
}
