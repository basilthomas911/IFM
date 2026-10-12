using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: FinancialAuthorityReference; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record FinancialAuthorityReference
{
    [Key(0)] public long PortfolioVersion { get; init; }
    [Key(1)] public long FundMandateVersion { get; init; }
    [Key(2)] public int PolicyId { get; init; }
    [Key(3)] public long PolicyVersion { get; init; }
    [Key(4)] public Guid EnvelopeId { get; init; }
    [Key(5)] public long EnvelopeVersion { get; init; }
    [Key(6)] public long AssignmentVersion { get; init; }
    [Key(7)] public CatalogKey DeploymentKey { get; init; }
    [Key(8)] public long AuthorityEpoch { get; init; }
    [Key(9)] public string ValuationWatermark { get; init; } = string.Empty;
    [Key(10)] public string SourceWatermark { get; init; } = string.Empty;
    [Key(11)] public string FinancialSnapshotHash { get; init; } = string.Empty;
    [Key(12)] public DateTime ValidUntilUtc { get; init; }
}
