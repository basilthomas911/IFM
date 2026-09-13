using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Model;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Extensions;

public static class TradeOrderQueryHandlers
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
