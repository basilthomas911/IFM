using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;

/// <summary>Provides shared projection reads for strategy-specific position queries.</summary>
internal static class PositionQueryModel
{
    /// <summary>Reads one position and filters it to the required strategy kind.</summary>
    internal static async ValueTask OneAsync(
        StrategyPositionQueryBase<StrategyPositionSnapshot> query,
        IFuturesOptionPositionQueryContext context,
        TradeStrategyKind strategyKind,
        CancellationToken cancellationToken)
    {
        var value = await context.DbFactory.TradeDb.GetStrategyPositionAsync(query.PositionId, cancellationToken);
        if (value?.StrategyKind != strategyKind)
            value = null;
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<StrategyPositionSnapshot?>(value));
    }

    /// <summary>Reads a position history page and filters it to the required strategy kind.</summary>
    internal static async ValueTask ManyAsync(
        PositionHistoryQuery query,
        IFuturesOptionPositionQueryContext context,
        TradeStrategyKind strategyKind,
        CancellationToken cancellationToken)
    {
        var positions = new List<StrategyPositionSnapshot>();
        var pagingState = query.PagingState;
        do
        {
            var page = await context.DbFactory.TradeDb.GetStrategyPositionHistoryAsync(
                query.PositionId.PositionId, query.FromUtc, query.ToUtc, query.PageSize, pagingState, cancellationToken);
            positions.AddRange(page.Items.Where(x => x.StrategyKind == strategyKind && x.Id == query.PositionId));
            pagingState = page.PagingState;
        } while (query.LoadAll && pagingState is { Length: > 0 });
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<StrategyPositionSnapshot[]>(positions.ToArray()));
    }
}
