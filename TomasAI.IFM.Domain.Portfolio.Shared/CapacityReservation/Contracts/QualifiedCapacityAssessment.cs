using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Independent, immutable result loaded from the event store inside the financial transaction.</summary>
[MessagePackObject]
public sealed record QualifiedCapacityAssessment
{
    [Key(0)] public Guid InvocationId { get; init; }
    [Key(1)] public Guid ResultId { get; init; }
    [Key(2)] public string ResultHash { get; init; } = string.Empty;
    [Key(3)] public int PortfolioId { get; init; }
    [Key(4)] public int FundId { get; init; }
    [Key(5)] public Guid WorkflowId { get; init; }
    [Key(6)] public long WorkflowRevision { get; init; }
    [Key(7)] public Guid CompositionResultId { get; init; }
    [Key(8)] public string CompositionResultHash { get; init; } = string.Empty;
    [Key(9)] public string UnitCandidateHash { get; init; } = string.Empty;
    [Key(10)] public string SizedOrderHash { get; init; } = string.Empty;
    [Key(11)] public int StrategyUnits { get; init; }
    [Key(12)] public CapacityRequirements Requirements { get; init; } = new();
    [Key(13)] public FinancialAuthorityReference Authority { get; init; } = new();
    [Key(14)] public FinancialEvidenceReference MarginEvidence { get; init; } = new();
    [Key(15)] public string Environment { get; init; } = string.Empty;
    [Key(16)] public DateTime ValidUntilUtc { get; init; }
    [Key(17)] public bool Eligible { get; init; }
}
