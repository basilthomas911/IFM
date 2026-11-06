using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Query;

public static class GetOrderExecution
{

    extension(GetOrderExecutionQuery query)
    {
        /// <summary>Reads the projection for the requested order execution attempt.</summary>
        public Task<OrderExecutionDefinition?> ExecuteAsync(ITradeDbReadContext tradeDb, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tradeDb);
            ArgumentNullException.ThrowIfNull(query);
            return tradeDb.GetOrderExecutionAsync(query.TradeOrderId, query.ExecutionAttemptId, cancellationToken);
        }
    }
}
