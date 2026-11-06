using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradeQuantity
{

    extension(GetTradeQuantityQuery query)
    {
        /// <summary>Reads the average option-leg quantity for a legacy option trade.</summary>
        public async ValueTask<ServiceResult<ScalarReadModel<int>>> ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await context.DbFactory.GetTradeQuantityAsync(query.TradeId, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<ScalarReadModel<int>>(result);
        }
    }
}
