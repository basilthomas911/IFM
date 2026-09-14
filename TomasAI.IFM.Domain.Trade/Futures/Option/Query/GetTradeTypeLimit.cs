using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetTradeTypeLimit
{
    /// <summary>Reads the strategy-specific limit configuration for a legacy option trade.</summary>
    public static async ValueTask ExecuteAsync(
        this GetTradeTypeLimitQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        var result = await context.DbFactory
            .GetTradeTypeLimitAsync(query.TradeId, query.TradeType, cancellationToken)
            .ConfigureAwait(false) ?? new TradeTypeLimitReadModel();
        await context.ReplyAsync(
            query.Subject.ThreadId,
            query.Subject.Verb,
            new ServiceResult<TradeTypeLimitReadModel>(result)).ConfigureAwait(false);
    }
}
