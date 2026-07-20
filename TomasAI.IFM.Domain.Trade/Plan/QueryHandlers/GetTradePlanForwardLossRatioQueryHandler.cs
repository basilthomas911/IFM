using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

namespace TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;

public class GetTradePlanForwardLossRatioQueryHandler(ITradeDbContext db) 
    : BaseQueryHandler,
    IAsyncQueryHandler<GetTradePlanForwardLossRatioQuery, TradePlanForwardLossRatioReadModel>
{
    /// <summary>
    /// Executes the query to retrieve the forward loss ratio for a trade plan.
    /// </summary>
    /// <param name="q">The query containing the parameters required to fetch the forward loss ratio, including the value date.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a  <see
    /// cref="TradePlanForwardLossRatioReadModel"/> representing the forward loss ratio for the specified trade plan.</returns>
    public async Task<TradePlanForwardLossRatioReadModel> ExecuteAsync(GetTradePlanForwardLossRatioQuery q)
        => await ExecuteQueryAsync(q, () => db.GetTradePlanForwardLossRatioAsync(q.ValueDate)!);
}
