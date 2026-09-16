using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Query;

public static class GetTradeOrder
{
    public static async ValueTask ExecuteAsync(
        this GetTradeOrderQuery query,
        ITradeOrderQueryContext context,
        CancellationToken cancellationToken)
    {
        var value = await context.DbFactory.TradeDb
            .GetTradeOrderAsync(query.TradeOrderId, cancellationToken)
            .ConfigureAwait(false);
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<TradeOrderDefinition?>(value)).ConfigureAwait(false);
    }
}
