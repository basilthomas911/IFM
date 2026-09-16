using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;

/// <summary>Handles <see cref="GetIronCondorOptionTradePositionHistoryQuery"/>.</summary>
public static class GetIronCondorOptionTradePositionHistory
{
    /// <summary>Reads and replies with the requested IronCondor position data.</summary>
    public static ValueTask ExecuteAsync(this GetIronCondorOptionTradePositionHistoryQuery query, IFuturesOptionPositionQueryContext context, CancellationToken cancellationToken)
        => PositionQueryModel.ManyAsync(query, context, TradeStrategyKind.IronCondor, cancellationToken);
}
