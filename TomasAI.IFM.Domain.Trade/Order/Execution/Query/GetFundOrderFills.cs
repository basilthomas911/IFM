using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Query;

public static class GetFundOrderFills
{
    extension(GetFundOrderFillsQuery query)
    {
        /// <summary>Reads committed Fund execution evidence through the TradeDb projection.</summary>
        public Task<FundOrderFillsRow[]> ExecuteAsync(ITradeDbReadContext tradeDb, CancellationToken cancellationToken)
        {
            if (query.PortfolioId <= 0 || query.FundId <= 0 || query.ValueDate == default ||
                query.AfterOrderId < 0 || query.AfterOrderId == 0 && query.AfterAttemptId != Guid.Empty)
                throw new ArgumentException("Valid Fund, ValueDate and page cursor are required.", nameof(query));
            return tradeDb.GetFundOrderFillsAsync(query.PortfolioId, query.FundId, query.ValueDate,
                query.AfterOrderId, query.AfterAttemptId, cancellationToken);
        }
    }
}
