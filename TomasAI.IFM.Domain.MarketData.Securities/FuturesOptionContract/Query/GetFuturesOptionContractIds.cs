using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContractIds
{

    extension(GetFuturesOptionContractIdsQuery q)
    {
        /// <summary>
        /// Handles a request to retrieve existing futures option contract IDs from a list of contract IDs.
        /// </summary>
        public ValueTask<string[]> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            return dbFactory.GetFuturesOptionContractIdsAsync(q.ContractIds, cancellationToken);
        }
    }
}
