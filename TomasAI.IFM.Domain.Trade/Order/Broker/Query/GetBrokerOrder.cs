using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query;
/// <summary>Handles the single broker-order detail query.</summary>
public static class GetBrokerOrder
{

    extension(GetBrokerOrderQuery query)
    {
        /// <summary>Returns the latest committed projection or an empty definition when unavailable.</summary>
        public async ValueTask ExecuteAsync(IBrokerOrderQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            context.Store.TryGet(query.BrokerOrderId, out var value);
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<BrokerOrderDefinition?>(value)).ConfigureAwait(false);
        }
    }
}
