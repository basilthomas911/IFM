using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionTrades
{
    /// <summary>Reads the legacy option-trade projections for an order.</summary>
    public static async ValueTask ExecuteAsync(
        this GetOptionTradesQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        OptionTradeReadModel[] result = [.. await context.DbFactory.TradeDb
            .GetOptionTradesAsync(query.OrderId, cancellationToken).ConfigureAwait(false)];
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<OptionTradeReadModel[]>(result)).ConfigureAwait(false);
    }
}
