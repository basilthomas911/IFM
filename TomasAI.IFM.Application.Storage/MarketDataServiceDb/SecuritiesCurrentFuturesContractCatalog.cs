using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

public sealed class SecuritiesCurrentFuturesContractCatalog(ISecuritiesDbContext securities)
    : ICurrentFuturesContractCatalog
{
    public async Task<IReadOnlyList<FuturesContractV3ReadModel>> GetByRootAsync(
        string rootSymbol, CancellationToken cancellationToken)
        => [.. await securities.GetFuturesContractsBySymbolAsync(rootSymbol, cancellationToken).ConfigureAwait(false)];
}
