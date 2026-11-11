using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface ICurrentFuturesContractCatalog
{
    /// <summary>Reads futures contracts by symbol from the committed projection.</summary>
    /// <param name="symbol">The root symbol used to select the projection rows.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The selected rows in the query's defined order.</returns>
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken);
}
