using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContractIds
{
    /// <summary>
    /// Handles a request to retrieve existing futures option contract IDs from a list of contract IDs.
    /// </summary>
    public static async ValueTask GetFuturesOptionContractIdsAsync(this GetFuturesOptionContractIdsQuery q, IQueryActorContext context, IDbContextFactory dbFactory)
    {
        var existingContractIds = new List<string>();
        var db = dbFactory.SecuritiesDb;
        foreach (var contractId in q.ContractIds)
        {
            if (await db.GetFuturesOptionContractAsync(contractId) is not null)
                existingContractIds.Add(contractId);
        }
        await context.ReplyAsync(q.Subject.ThreadId, GetFuturesOptionContractIdsQuery.Verb, new ServiceResult<string[]>([.. existingContractIds]));
    }
}
