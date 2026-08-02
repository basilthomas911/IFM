using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContractIds
{
    /// <summary>
    /// Handles a request to retrieve existing futures option contract IDs from a list of contract IDs.
    /// </summary>
    public static async ValueTask<string[]> GetFuturesOptionContractIdsAsync(
        this GetFuturesOptionContractIdsQuery q, IDbContextFactory dbFactory)
    {
        var existingContractIds = new List<string>();
        var db = dbFactory.SecuritiesDb;
        foreach (var contractId in q.ContractIds)
        {
            if (await db.GetFuturesOptionContractAsync(contractId) is not null)
                existingContractIds.Add(contractId);
        }
        return [.. existingContractIds];
    }
}
