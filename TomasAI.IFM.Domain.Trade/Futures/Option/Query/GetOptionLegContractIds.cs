using TomasAI.IFM.Domain.Trade.Futures.Option.Query.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Query;

public static class GetOptionLegContractIds
{

    extension(GetOptionLegContractIdsQuery query)
    {
        /// <summary>Reads all contract identifiers associated with an option trade.</summary>
        public async ValueTask ExecuteAsync(IFuturesOptionTradeQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            string[] result = [.. await context.DbFactory.TradeDb.GetOptionLegContractIdsAsync(query.TradeId, cancellationToken).ConfigureAwait(false)];
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceResult<string[]>(result)).ConfigureAwait(false);
        }
    }
}
