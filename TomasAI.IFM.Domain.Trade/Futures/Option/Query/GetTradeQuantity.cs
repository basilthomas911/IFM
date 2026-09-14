using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradeQuantity
{
    /// <summary>Reads the average option-leg quantity for a legacy option trade.</summary>
    public static async ValueTask ExecuteAsync(
        this GetTradeQuantityQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.DbFactory
            .GetTradeQuantityAsync(query.TradeId, cancellationToken)
            .ConfigureAwait(false);
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<ScalarReadModel<int>>(result)).ConfigureAwait(false);
    }
}
