using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Option.Query;

internal static class GetOptionTrade
{
    /// <summary>
    /// Gets the option trade details for a given trade ID.
    /// </summary>
    /// <param name="q">The query containing the trade ID for which to retrieve option trade details.</param>
    /// <param name="context">The query actor context used to send the reply containing the result.</param>
    /// <param name="dbFactory">The database context factory used to access option trade data.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    internal static async ValueTask GetOptionTradeAsync(this GetOptionTradeQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.TradeDb.GetOptionTradeAsync(q.OrderId, q.TradeId);
        await context.ReplyAsync(q.Subject.ThreadId, GetOptionTradeQuery.Verb, new ServiceResult<OptionTradeReadModel?>(result));
    }
}
