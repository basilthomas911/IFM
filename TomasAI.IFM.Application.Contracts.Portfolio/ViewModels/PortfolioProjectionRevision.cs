using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

/// <summary>Describes the persisted revision of a portfolio or fund projection.</summary>
public sealed record PortfolioProjectionRevision(
    int PortfolioId,
    int? FundId,
    long AggregateRevision,
    long SourceEventId);
