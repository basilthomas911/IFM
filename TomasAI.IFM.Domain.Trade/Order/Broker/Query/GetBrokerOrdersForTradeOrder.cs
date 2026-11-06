using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query;
/// <summary>Handles the Trade Order scoped broker-order list query.</summary>
public static class GetBrokerOrdersForTradeOrder
{

    extension(GetBrokerOrdersForTradeOrderQuery query)
    {
        /// <summary>Returns every latest component order in stable execution/component order.</summary>
        public async ValueTask<ServiceResult<BrokerOrderDefinition[]>> ExecuteAsync(IBrokerOrderQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var values = context.Store.List(query.TradeOrderId);
            return new ServiceOk<BrokerOrderDefinition[]>(values);
        }
    }
}
