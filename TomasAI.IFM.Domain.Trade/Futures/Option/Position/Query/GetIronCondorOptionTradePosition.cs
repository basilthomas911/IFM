using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;
/// <summary>Handles <see cref = "GetIronCondorOptionTradePositionQuery"/>.</summary>
public static class GetIronCondorOptionTradePosition
{

    extension(GetIronCondorOptionTradePositionQuery query)
    {
        /// <summary>Reads the requested IronCondor position snapshot.</summary>
        public ValueTask<StrategyPositionSnapshot?> ExecuteAsync(ITradeDbReadContext tradeDb, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tradeDb);
            ArgumentNullException.ThrowIfNull(query);
            return PositionQueryModel.OneAsync(query, tradeDb, TradeStrategyKind.IronCondor, cancellationToken);
        }
    }
}
