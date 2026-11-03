using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContractsPage
{

    extension(GetFuturesOptionContractsPageQuery query)
    {
        public Task<FuturesOptionContractPageReadModel> ExecuteAsync(
        IDbContextFactory factory, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(query);
            query.Request.Validate();
            return factory.SecuritiesDb.GetFuturesOptionContractsPageAsync(query.Request, cancellationToken);
        }
    }
}
