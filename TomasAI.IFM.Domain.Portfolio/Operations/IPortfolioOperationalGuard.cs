using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Operations;

public interface IPortfolioOperationalGuard
{
    PortfolioAccessContext Demand(PortfolioOperation operation, PortfolioAccessContext access, bool mutation);
    PortfolioOperationalOptions Options { get; }
}
