using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

public interface IPortfolioProjectionRebuilder
{
    Task<PortfolioProjectionRebuildResult> RebuildAsync(PortfolioProjectionRebuildRequest request, CancellationToken cancellationToken = default);
}
