using TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Query;

public static class GetFuturesTradePosition
{
    /// <summary>
    /// Reads the current strategy-position snapshot and returns it only when it is a futures outright position.
    /// </summary>
    /// <param name="query">The query containing the strategy-position identity.</param>
    /// <param name="context">The typed query context that provides storage and reply operations.</param>
    /// <param name="cancellationToken">The token that cancels the storage operation.</param>
    /// <returns>A task that completes after the typed query reply is sent.</returns>
    public static async ValueTask ExecuteAsync(
        this GetFuturesTradePositionQuery query,
        IFuturesPositionQueryContext context,
        CancellationToken cancellationToken)
    {
        var position = await context.DbFactory.TradeDb
            .GetStrategyPositionAsync(query.PositionId, cancellationToken)
            .ConfigureAwait(false);

        if (position?.StrategyKind != TradeStrategyKind.FuturesOutright)
            position = null;

        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<StrategyPositionSnapshot?>(position)).ConfigureAwait(false);
    }
}
