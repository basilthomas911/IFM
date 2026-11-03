using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;
/// <summary>Handles <see cref = "GetVerticalSpreadOptionTradePositionHistoryQuery"/>.</summary>
public static class GetVerticalSpreadOptionTradePositionHistory
{

    extension(GetVerticalSpreadOptionTradePositionHistoryQuery query)
    {
        /// <summary>Reads and replies with the requested VerticalSpread position data.</summary>
        public ValueTask ExecuteAsync(IFuturesOptionPositionQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            return PositionQueryModel.ManyAsync(query, context, TradeStrategyKind.VerticalSpread, cancellationToken);
        }
    }
}
