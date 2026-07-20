using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

namespace TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;

public class GetTradePlanForwardLossRatiosQueryHandler(ITradeDbContext db)
    : BaseQueryHandler,
    IAsyncQueryHandler<GetTradePlanForwardLossRatiosQuery, TradePlanForwardLossRatioReadModel[]>
{
    /// <summary>
    /// Executes the query to retrieve forward loss ratios for trade plans.
    /// </summary>
    /// <param name="q">The query containing the parameters required to fetch the forward loss ratios for trade plans.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of  <see
    /// cref="TradePlanForwardLossRatioReadModel"/> objects representing the forward loss ratios for the specified trade
    /// plans.</returns>
    public async Task<TradePlanForwardLossRatioReadModel[]> ExecuteAsync(GetTradePlanForwardLossRatiosQuery q)
        => await ExecuteQueryAsync(q, () => GetTradePlanForwardLossRatiosAsync(q));

    async Task<TradePlanForwardLossRatioReadModel[]> GetTradePlanForwardLossRatiosAsync(GetTradePlanForwardLossRatiosQuery q)
        => [.. await db.GetTradePlanForwardLossRatiosAsync(q.StartDate, q.EndDate)];
}
