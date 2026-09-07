using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Query;

public static class GetFuturesOptionContractsPage
{
    public static Task<FuturesOptionContractPageReadModel> GetFuturesOptionContractsPageAsync(
        this GetFuturesOptionContractsPageQuery query, IDbContextFactory factory, CancellationToken cancellationToken = default)
    {
        query.Request.Validate();
        return factory.SecuritiesDb.GetFuturesOptionContractsPageAsync(query.Request, cancellationToken);
    }
}
