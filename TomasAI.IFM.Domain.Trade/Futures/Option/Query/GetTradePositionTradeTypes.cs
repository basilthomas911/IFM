using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradePositionTradeTypes
{
    /// <summary>Reads the legacy strategy types represented by an option position.</summary>
    public static async ValueTask ExecuteAsync(
        this GetTradePositionTradeTypesQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        string[] result = [.. await context.DbFactory.TradeDb.GetTradePositionTradeTypesAsync(
            query.OrderId, query.TradeId, query.ValueDate, query.TradeStatus,
            query.DaysToExpiry, cancellationToken).ConfigureAwait(false)];
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<string[]>(result)).ConfigureAwait(false);
    }
}
