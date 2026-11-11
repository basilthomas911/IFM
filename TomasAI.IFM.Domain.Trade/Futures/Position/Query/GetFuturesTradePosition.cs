using TomasAI.IFM.Domain.Trade.Futures.Position.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Position.Query;

public static class GetFuturesTradePosition
{

    /// <param name="query">
    /// The query containing the strategy-position identity.
    /// </param>
    extension(GetFuturesTradePositionQuery query)
    {
        /// <summary>
        /// Reads the current strategy-position snapshot and returns it only when it is a futures outright position.
        /// </summary>
        ///
        /// <param name = "context">The typed query context that provides projection storage.</param>
        /// <param name = "cancellationToken">The token that cancels the storage operation.</param>
        /// <returns>The typed projected result.</returns>
        public async ValueTask<ServiceResult<StrategyPositionSnapshot?>> ExecuteAsync(IFuturesPositionQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            if (!query.PositionId.IsValid)
                throw new ArgumentException("Valid position identity is required.", nameof(query));
            var position = await context.DbFactory.TradeDb.GetStrategyPositionAsync(query.PositionId, cancellationToken).ConfigureAwait(false);
            if (position?.StrategyKind != TradeStrategyKind.FuturesOutright)
                position = null;
            return new ServiceResult<StrategyPositionSnapshot?>(position);
        }
    }
}
