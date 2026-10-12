using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface ICurrentFuturesContractCatalog
{
    Task<ICollection<FuturesContractV3ReadModel>> GetFuturesContractsBySymbolAsync(
        string symbol,
        CancellationToken cancellationToken);
}
