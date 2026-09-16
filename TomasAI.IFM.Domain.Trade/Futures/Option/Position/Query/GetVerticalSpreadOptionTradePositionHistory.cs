using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;

/// <summary>Handles <see cref="GetVerticalSpreadOptionTradePositionHistoryQuery"/>.</summary>
public static class GetVerticalSpreadOptionTradePositionHistory
{
    /// <summary>Reads and replies with the requested VerticalSpread position data.</summary>
    public static ValueTask ExecuteAsync(this GetVerticalSpreadOptionTradePositionHistoryQuery query, IFuturesOptionPositionQueryContext context, CancellationToken cancellationToken)
        => PositionQueryModel.ManyAsync(query, context, TradeStrategyKind.VerticalSpread, cancellationToken);
}
