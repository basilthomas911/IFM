using TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Query;

public static class GetFuturesTradePositionHistory
{
    /// <summary>
    /// Reads the requested history page and returns only futures outright position snapshots.
    /// </summary>
    /// <param name="query">The query containing the position identity, date range, and paging criteria.</param>
    /// <param name="context">The typed query context that provides storage and reply operations.</param>
    /// <param name="cancellationToken">The token that cancels the storage operation.</param>
    /// <returns>A task that completes after the typed query reply is sent.</returns>
    public static async ValueTask ExecuteAsync(
        this GetFuturesTradePositionHistoryQuery query,
        IFuturesPositionQueryContext context,
        CancellationToken cancellationToken)
    {
        var page = await context.DbFactory.TradeDb.GetStrategyPositionHistoryAsync(
            query.PositionId,
            query.FromUtc,
            query.ToUtc,
            query.PageSize,
            query.PagingState,
            cancellationToken).ConfigureAwait(false);

        var positions = page.Items
            .Where(static position =>
                position.StrategyKind == TradeStrategyKind.FuturesOutright)
            .ToArray();

        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<StrategyPositionSnapshot[]>(positions)).ConfigureAwait(false);
    }
}
