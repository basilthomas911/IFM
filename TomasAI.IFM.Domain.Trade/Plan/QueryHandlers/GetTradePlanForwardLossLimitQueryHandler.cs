using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

namespace TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;

public class GetTradePlanForwardLossLimitQueryHandler(ITradeDbContext db) 
    : BaseQueryHandler,
   IAsyncQueryHandler<GetTradePlanForwardLossLimitQuery, TradePlanForwardLossLimitReadModel>
{
    /// <summary>
    /// Executes the query to retrieve the forward loss limit details for a trade plan.
    /// </summary>
    /// <remarks>This method asynchronously executes the query and retrieves the forward loss limit details 
    /// from the database. Ensure that the query parameters are valid and properly initialized before  calling this
    /// method.</remarks>
    /// <param name="q">The query containing the parameters required to fetch the forward loss limit details, including  the order ID,
    /// trade ID, value date, and trade type.</param>
    /// <returns>A <see cref="TradePlanForwardLossLimitReadModel"/> representing the forward loss limit details  for the
    /// specified trade plan.</returns>
    public async Task<TradePlanForwardLossLimitReadModel> ExecuteAsync(GetTradePlanForwardLossLimitQuery q)
        => await ExecuteQueryAsync(q, () => db.GetTradePlanForwardLossLimitAsync(q.OrderId, q.TradeId, q.ValueDate, q.TradeType)!);

}
