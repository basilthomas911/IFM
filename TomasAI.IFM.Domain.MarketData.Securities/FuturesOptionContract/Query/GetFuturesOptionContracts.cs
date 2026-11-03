using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContracts
{

    extension(GetFuturesOptionContractsQuery q)
    {
        /// <summary>
        /// Handles a request to retrieve all futures option contracts by symbol.
        /// </summary>
        public async ValueTask<FuturesOptionContractReadModel[]> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(q);
            return [.. await (dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(q.Symbol, cancellationToken)).ConfigureAwait(false)];
        }
    }
}
