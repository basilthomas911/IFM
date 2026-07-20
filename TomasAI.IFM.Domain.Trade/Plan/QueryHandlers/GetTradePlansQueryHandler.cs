using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;

namespace TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;

public class GetTradePlansQueryHandler(ITradeDbContext db) 
    : BaseQueryHandler,
    IAsyncQueryHandler<GetTradePlansQuery, TradePlanReadModel[]>
{
    /// <summary>
    /// Executes the specified query asynchronously and retrieves an array of trade plan view models.
    /// </summary>
    /// <param name="q">The query parameters used to filter and retrieve trade plans. Cannot be <c>null</c>.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an array of <see
    /// cref="TradePlanReadModel"/> objects matching the query criteria. The array is empty if no trade plans are found.</returns>
    public async Task<TradePlanReadModel[]> ExecuteAsync(GetTradePlansQuery q)
        => await ExecuteQueryAsync(q, () => GetTradePlansAsync(q));

    async Task<TradePlanReadModel[]> GetTradePlansAsync(GetTradePlansQuery q)
    {
        var tradePlans = await db.GetTradePlansAsync(q.OrderId, q.TradeId, q.ValueDate);
        if ( tradePlans.Count == 0)
            tradePlans = await db.GetLastTradePlansAsync(q.OrderId, q.TradeId);
        return [.. tradePlans];
    }
}
