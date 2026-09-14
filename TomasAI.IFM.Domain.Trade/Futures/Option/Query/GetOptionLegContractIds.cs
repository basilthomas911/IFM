using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionLegContractIds
{
    /// <summary>Reads all contract identifiers associated with an option trade.</summary>
    public static async ValueTask ExecuteAsync(
        this GetOptionLegContractIdsQuery query,
        IFuturesOptionTradeQueryContext context,
        CancellationToken cancellationToken)
    {
        string[] result = [.. await context.DbFactory.TradeDb
            .GetOptionLegContractIdsAsync(query.TradeId, cancellationToken).ConfigureAwait(false)];
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<string[]>(result)).ConfigureAwait(false);
    }
}
