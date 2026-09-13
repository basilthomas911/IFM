using TomasAI.IFM.Domain.Trade.Order.Execution.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Query.Extensions;

public static class OrderExecutionQueryHandlers
{
    public static async ValueTask ExecuteAsync(
        this GetOrderExecutionQuery query,
        IOrderExecutionQueryContext context,
        CancellationToken cancellationToken)
    {
        var value = await context.DbFactory.TradeDb
            .GetOrderExecutionAsync(query.TradeOrderId, query.ExecutionAttemptId, cancellationToken)
            .ConfigureAwait(false);
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<OrderExecutionDefinition?>(value)).ConfigureAwait(false);
    }
}
