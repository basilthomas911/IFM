using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradeHistory
{
    /// <summary>Reads the ordered position history for a legacy option-trade order.</summary>
    public static async ValueTask ExecuteAsync(
        this GetTradeHistoryQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        TradeHistoryReadModel[] result =
            [.. await context.DbFactory.GetTradeHistoryAsync(query.OrderId, cancellationToken)
                .ConfigureAwait(false)];
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<TradeHistoryReadModel[]>(result)).ConfigureAwait(false);
    }
}
