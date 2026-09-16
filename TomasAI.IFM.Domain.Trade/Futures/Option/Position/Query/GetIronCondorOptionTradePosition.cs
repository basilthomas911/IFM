using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.Query;

/// <summary>Handles <see cref="GetIronCondorOptionTradePositionQuery"/>.</summary>
public static class GetIronCondorOptionTradePosition
{
    /// <summary>Reads and replies with the requested IronCondor position data.</summary>
    public static ValueTask ExecuteAsync(this GetIronCondorOptionTradePositionQuery query, IFuturesOptionPositionQueryContext context, CancellationToken cancellationToken)
        => PositionQueryModel.OneAsync(query, context, TradeStrategyKind.IronCondor, cancellationToken);
}
