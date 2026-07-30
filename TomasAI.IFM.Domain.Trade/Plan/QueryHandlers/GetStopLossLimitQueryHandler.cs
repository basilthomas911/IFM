using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.TradeDb;

namespace TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;

public class GetStopLossLimitQueryHandler(ITradeDbContext db) 
    : BaseQueryHandler,
    IAsyncQueryHandler<GetStopLossLimitQuery, TradePlanStopLossLimitReadModel>
{
    /// <summary>
    /// Executes the query to retrieve stop-loss and limit information for a trade plan.
    /// </summary>
    /// <remarks>This method asynchronously retrieves stop-loss and limit information for a trade plan  based
    /// on the provided query parameters. Ensure that the <paramref name="q"/> object contains  valid identifiers for
    /// the order and trade before calling this method.</remarks>
    /// <param name="q">The query containing the identifiers for the order and trade.</param>
    /// <returns>A <see cref="TradePlanStopLossLimitReadModel"/> representing the stop-loss and limit details  for the specified
    /// trade plan.</returns>
    public async Task<TradePlanStopLossLimitReadModel> ExecuteAsync(GetStopLossLimitQuery q)
        =>await ExecuteQueryAsync(q, () => db.GetTradePlanStopLossLimitAsync(q.OrderId, q.TradeId)!);
}
