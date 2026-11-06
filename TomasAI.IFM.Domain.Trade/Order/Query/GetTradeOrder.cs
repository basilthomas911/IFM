using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Query;

public static class GetTradeOrder
{

    extension(GetTradeOrderQuery query)
    {
        public async ValueTask<ServiceResult<TradeOrderDefinition?>> ExecuteAsync(ITradeOrderQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var value = await context.DbFactory.TradeDb.GetTradeOrderAsync(query.TradeOrderId, cancellationToken).ConfigureAwait(false);
            return new ServiceResult<TradeOrderDefinition?>(value);
        }
    }
}
