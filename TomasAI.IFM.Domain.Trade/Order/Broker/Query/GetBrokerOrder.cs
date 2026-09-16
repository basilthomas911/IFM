using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Broker.Query;

/// <summary>Handles the single broker-order detail query.</summary>
public static class GetBrokerOrder
{
    /// <summary>Returns the latest committed projection or an empty definition when unavailable.</summary>
    public static async ValueTask ExecuteAsync(
        this GetBrokerOrderQuery query,
        IBrokerOrderQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        context.Store.TryGet(query.BrokerOrderId, out var value);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<BrokerOrderDefinition?>(value)).ConfigureAwait(false);
    }
}
