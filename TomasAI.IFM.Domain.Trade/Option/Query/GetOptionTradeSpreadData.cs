using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Query;

internal static class GetOptionTradeSpreadData
{
    /// <summary>
    /// Gets the option trade spread data.
    /// </summary>
    /// <param name="q">The query containing the parameters for retrieving option trade spread data, including order ID, trade ID, value date, and trade type.</param>
    /// <param name="context">The query actor context used to send the reply containing the result.</param>
    /// <param name="dbFactory">The database context factory used to create a context for accessing option trade spread data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async ValueTask GetOptionTradeSpreadDataAsync(this GetOptionTradeSpreadDataQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.TradeDb.GetOptionTradeSpreadDataAsync(q.OrderId, q.TradeId, q.ValueDate, q.TradeType);
        await context.ReplyAsync(q.Subject.ThreadId, GetOptionTradeSpreadDataQuery.Verb, new ServiceResult<OptionTradeSpreadsDataModel>(result));
    }
}
