using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;
/// <summary>Handles <see cref = "GetVerticalSpreadOptionTradePositionQuery"/>.</summary>
public static class GetVerticalSpreadOptionTradePosition
{

    extension(GetVerticalSpreadOptionTradePositionQuery query)
    {
        /// <summary>Reads the requested VerticalSpread position snapshot.</summary>
        public ValueTask<StrategyPositionSnapshot?> ExecuteAsync(ITradeDbReadContext tradeDb, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tradeDb);
            ArgumentNullException.ThrowIfNull(query);
            return PositionQueryModel.OneAsync(query, tradeDb, TradeStrategyKind.VerticalSpread, cancellationToken);
        }
    }
}
