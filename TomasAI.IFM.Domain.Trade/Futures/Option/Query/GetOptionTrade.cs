using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionTrade
{
    /// <summary>Reads one legacy option-trade projection by order and trade identifier.</summary>
    public static async ValueTask ExecuteAsync(
        this GetOptionTradeQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.DbFactory.TradeDb
            .GetOptionTradeAsync(query.OrderId, query.TradeId, cancellationToken)
            .ConfigureAwait(false);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<OptionTradeReadModel?>(result)).ConfigureAwait(false);
    }
}
