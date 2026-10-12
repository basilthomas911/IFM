using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public interface IDatabentoContractAuthority
{
    Task<IReadOnlyList<FuturesRolloverContractAssignment>> ReconcileAsync(DateOnly valueDate,
        string changedBy, CancellationToken cancellationToken);
}
