using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradeLimit
{

    extension(GetTradeLimitQuery query)
    {
        /// <summary>Reads the limit configuration for a legacy option trade.</summary>
        public async ValueTask<ServiceResult<TradeLimitReadModel>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await context.DbFactory.GetTradeLimitAsync(query.TradeId, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<TradeLimitReadModel>(result);
        }
    }
}
