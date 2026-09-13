using TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Query.Extensions;

public static class FuturesPositionQueryHandlers
{
    public static async ValueTask ExecuteAsync(
        this GetFuturesTradePositionQuery query,
        IFuturesPositionQueryContext context,
        CancellationToken cancellationToken)
    {
        var value = await context.DbFactory.TradeDb
            .GetStrategyPositionAsync(query.PositionId, cancellationToken)
            .ConfigureAwait(false);
        if (value?.StrategyKind != TradeStrategyKind.FuturesOutright) value = null;
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<StrategyPositionSnapshot?>(value)).ConfigureAwait(false);
    }

    public static async ValueTask ExecuteAsync(
        this GetFuturesTradePositionHistoryQuery query,
        IFuturesPositionQueryContext context,
        CancellationToken cancellationToken)
    {
        var page = await context.DbFactory.TradeDb.GetStrategyPositionHistoryAsync(
            query.PositionId, query.FromUtc, query.ToUtc,
            query.PageSize, query.PagingState, cancellationToken).ConfigureAwait(false);
        var items = page.Items
            .Where(static position => position.StrategyKind == TradeStrategyKind.FuturesOutright)
            .ToArray();
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<StrategyPositionSnapshot[]>(items)).ConfigureAwait(false);
    }
}
