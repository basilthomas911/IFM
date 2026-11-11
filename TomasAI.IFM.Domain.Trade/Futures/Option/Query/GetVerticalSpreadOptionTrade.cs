using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetVerticalSpreadOptionTrade
{

    /// <param name="query">
    /// The query containing the globally unique trade identity.
    /// </param>
    extension(GetVerticalSpreadOptionTradeQuery query)
    {
        /// <summary>
        /// Reads one established trade and returns it only when it is a Vertical Spread.
        /// </summary>
        ///
        /// <param name = "context">The typed query context that provides projection storage.</param>
        /// <param name = "cancellationToken">The token that cancels the storage operation.</param>
        /// <returns>The typed projected result.</returns>
        public async ValueTask<ServiceResult<EstablishedTradeDefinition?>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            if (!query.TradeId.IsValid)
                throw new ArgumentException("Valid trade identity is required.", nameof(query));
            var trade = await context.DbFactory.TradeDb.GetEstablishedTradeAsync(query.TradeId, cancellationToken).ConfigureAwait(false);
            if (trade?.StrategyKind != TradeStrategyKind.VerticalSpread)
                trade = null;
            return new ServiceResult<EstablishedTradeDefinition?>(trade);
        }
    }
}
