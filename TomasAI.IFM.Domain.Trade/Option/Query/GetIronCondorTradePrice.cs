using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Query;

internal static class GetIronCondorTradePrice
{
    /// <summary>
    /// Gets the price of an Iron Condor trade based on the provided query.
    /// </summary>
    /// <param name="q"> </param>
    /// <param name="context"> </param>
    /// <param name="dbFactory"> </param>
    /// <returns></returns>
    internal static async ValueTask GetIronCondorTradePriceAsync(this GetIronCondorTradePriceQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var result = await dbFactory.TradeDb.GetIronCondorTradePriceAsync(q.TradeId, q.ValueDate);
        await context.ReplyAsync(q.Subject.ThreadId, GetIronCondorTradePriceQuery.Verb, new ServiceResult<TradePriceReadModel?>(result));
    }
}
