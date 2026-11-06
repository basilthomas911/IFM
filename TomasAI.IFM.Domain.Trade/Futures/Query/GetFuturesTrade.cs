using TomasAI.IFM.Domain.Trade.Futures.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Query;

public static class GetFuturesTrade
{

    /// <param name="query">
    /// The query containing the globally unique trade identity.
    /// </param>
    extension(GetFuturesTradeQuery query)
    {
        /// <summary>
        /// Reads one established trade and returns it only when it belongs to the futures asset family.
        /// </summary>
        ///
        /// <param name = "context">The typed query context that provides projection storage.</param>
        /// <param name = "cancellationToken">The token that cancels the storage operation.</param>
        /// <returns>The typed projected result.</returns>
        public async ValueTask<ServiceResult<EstablishedTradeDefinition?>> ExecuteAsync(IFuturesTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var trade = await context.DbFactory.TradeDb.GetEstablishedTradeAsync(query.TradeId, cancellationToken).ConfigureAwait(false);
            if (trade?.AssetFamily != TradeAssetFamily.Futures)
                trade = null;
            return new ServiceResult<EstablishedTradeDefinition?>(trade);
        }
    }
}
