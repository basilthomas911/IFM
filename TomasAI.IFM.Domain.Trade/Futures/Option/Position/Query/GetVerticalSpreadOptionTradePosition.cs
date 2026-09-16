using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;

/// <summary>Handles <see cref="GetVerticalSpreadOptionTradePositionQuery"/>.</summary>
public static class GetVerticalSpreadOptionTradePosition
{
    /// <summary>Reads and replies with the requested VerticalSpread position data.</summary>
    public static ValueTask ExecuteAsync(this GetVerticalSpreadOptionTradePositionQuery query, IFuturesOptionPositionQueryContext context, CancellationToken cancellationToken)
        => PositionQueryModel.OneAsync(query, context, TradeStrategyKind.VerticalSpread, cancellationToken);
}
