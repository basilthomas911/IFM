using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Option.Query;

internal static class GetOptionTrades
{
    /// <summary>
    /// Gets the option trades for a given option trade query.
    /// </summary>
    /// <param name="q">The query containing the parameters for retrieving option trades, including the order ID.</param>
    /// <param name="context">The query actor context used to send the reply containing the result.</param>
    /// <param name="dbFactory">The database context factory used to create a context for accessing option trade data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async ValueTask GetOptionTradesAsync(this GetOptionTradesQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.TradeDb.GetOptionTradesAsync(q.OrderId);
        await context.ReplyAsync(q.Subject.ThreadId, GetOptionTradesQuery.Verb, new ServiceResult<OptionTradeReadModel[]>([.. result]));
    }
}
