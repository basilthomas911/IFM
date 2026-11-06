using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;
/// <summary>Provides shared projection reads for strategy-specific position queries.</summary>
internal static class PositionQueryModel
{
    /// <summary>Reads one position and filters it to the required strategy kind.</summary>
    internal static async ValueTask<StrategyPositionSnapshot?> OneAsync(StrategyPositionQueryBase<StrategyPositionSnapshot> query, ITradeDbReadContext tradeDb, TradeStrategyKind strategyKind, CancellationToken cancellationToken)
    {
        var value = await tradeDb.GetStrategyPositionAsync(query.PositionId, cancellationToken).ConfigureAwait(false);
        return value?.StrategyKind == strategyKind ? value : null;
    }

    /// <summary>Reads a position history page and filters it to the required strategy kind.</summary>
    internal static async ValueTask<StrategyPositionSnapshot[]> ManyAsync(PositionHistoryQuery query, ITradeDbReadContext tradeDb, TradeStrategyKind strategyKind, CancellationToken cancellationToken)
    {
        var positions = new List<StrategyPositionSnapshot>();
        var pagingState = query.PagingState;
        do
        {
            var page = await tradeDb.GetStrategyPositionHistoryAsync(query.PositionId.PositionId, query.FromUtc, query.ToUtc, query.PageSize, pagingState, cancellationToken).ConfigureAwait(false);
            positions.AddRange(page.Items.Where(x => x.StrategyKind == strategyKind && x.Id == query.PositionId));
            pagingState = page.PagingState;
        }
        while (query.LoadAll && pagingState is { Length: > 0 });
        var trade = await tradeDb.GetEstablishedTradeAsync(query.PositionId.Trade, cancellationToken).ConfigureAwait(false);
        if (trade is not null && trade.StrategyKind == strategyKind)
            positions = positions.Select(p => StrategyPositionHistoryProjection.EnrichPosition(p, trade)).ToList();
        return positions.ToArray();
    }
}
