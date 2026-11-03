using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Query;

public static class GetFuturesContracts
{

    extension(GetFuturesContractsQuery q)
    {
        public async ValueTask<FuturesContractV3ReadModel[]> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)

        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            return [.. await (dbFactory.SecuritiesDb.GetFuturesContractsAsync(cancellationToken)).ConfigureAwait(false)];
        }
    }
}
