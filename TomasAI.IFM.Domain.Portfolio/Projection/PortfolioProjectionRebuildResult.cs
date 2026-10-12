using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

public sealed record PortfolioProjectionRebuildResult(
    int EventCount,
    long LastSourceEventId,
    string SourceCatalogSha256);
