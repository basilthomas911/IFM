using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Option.Query;

internal static class GetOptionTradeSpreadBarData
{
    /// <summary>
    /// Gets the option trade spread bar data.
    /// </summary>
    /// <param name="q">The query parameters specifying the order, trade, value date, trade type, and date range for retrieving option trade spread bar data.</param>
    /// <param name="context">The query actor context used to send the reply containing the result.</param>
    /// <param name="dbFactory">The database context factory used to access option trade spread bar data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async ValueTask GetOptionTradeSpreadBarDataAsync(this GetOptionTradeSpreadBarDataQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.TradeDb.GetOptionTradeSpreadBarDataAsync(q.OrderId, q.TradeId, q.ValueDate, q.TradeType, q.StartDate, q.EndDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetOptionTradeSpreadBarDataQuery.Verb, new ServiceResult<OptionTradeSpreadBarsDataModel[]>([.. result]));
    }
}
