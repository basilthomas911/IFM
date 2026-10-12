using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

public sealed record PortfolioProjectionRebuildRequest(
    IReadOnlyList<PortfolioId> Portfolios,
    IReadOnlyList<PortfolioFundId> Funds,
    IReadOnlyList<PortfolioFinancialPolicyId>? Policies = null);
