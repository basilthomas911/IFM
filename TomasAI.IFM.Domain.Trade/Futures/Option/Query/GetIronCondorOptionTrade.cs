using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetIronCondorOptionTrade
{

    /// <param name="query">
    /// The query containing the globally unique trade identity.
    /// </param>
    extension(GetIronCondorOptionTradeQuery query)
    {
        /// <summary>
        /// Reads one established trade and returns it only when it is an Iron Condor.
        /// </summary>
        ///
        /// <param name = "context">The typed query context that provides projection storage.</param>
        /// <param name = "cancellationToken">The token that cancels the storage operation.</param>
        /// <returns>The typed projected result.</returns>
        public async ValueTask<ServiceResult<EstablishedTradeDefinition?>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var trade = await context.DbFactory.TradeDb.GetEstablishedTradeAsync(query.TradeId, cancellationToken).ConfigureAwait(false);
            if (trade?.StrategyKind != TradeStrategyKind.IronCondor)
                trade = null;
            if (trade is not null) trade = StrategyPositionHistoryProjection.EnrichTrade(trade);
            return new ServiceResult<EstablishedTradeDefinition?>(trade);
        }
    }
}
