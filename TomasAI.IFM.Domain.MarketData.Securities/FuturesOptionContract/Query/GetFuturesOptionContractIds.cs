using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContractIds
{
    /// <summary>
    /// Handles a request to retrieve existing futures option contract IDs from a list of contract IDs.
    /// </summary>
    public static ValueTask<string[]> GetFuturesOptionContractIdsAsync(
        this GetFuturesOptionContractIdsQuery q,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
        => dbFactory.GetFuturesOptionContractIdsAsync(q.ContractIds, cancellationToken);
}
