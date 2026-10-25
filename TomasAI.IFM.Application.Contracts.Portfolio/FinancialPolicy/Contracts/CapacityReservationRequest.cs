using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: CapacityReservationRequest; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityReservationRequest
{
    [Key(0)] public Guid ReservationId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public int BookId { get; init; }
    [Key(3)] public int OrderId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(4)] public int[] TradeIds { get; init; } = [];
    [Key(5)] public Guid WorkflowId { get; init; }
    [Key(6)] public long InputWorkflowRevision { get; init; }
    [Key(7)] public Guid RiskInvocationId { get; init; }
    [Key(8)] public Guid RiskResultId { get; init; }
    [Key(9)] public string RiskAssessmentHash { get; init; } = string.Empty;
    [Key(10)] public Guid CompositionResultId { get; init; }
    [Key(11)] public string CompositionResultHash { get; init; } = string.Empty;
    [Key(12)] public string UnitCandidateHash { get; init; } = string.Empty;
    [Key(13)] public string SizedOrderHash { get; init; } = string.Empty;
    [Key(14)] public int StrategyUnits { get; init; }
    [Key(15)] public CapacityRequirements Requirements { get; init; } = new();
    [Key(16)] public FinancialAuthorityReference Authority { get; init; } = new();
    [Key(17)] public FinancialEvidenceReference MarginEvidenceReference { get; init; } = new();
    [Key(18)] public string ExecutionEnvironment { get; init; } = string.Empty;
    [Key(19)] public DateTime ValidUntilUtc { get; init; }
    [Key(20)] public string AcceptedIntentReference { get; init; } = string.Empty;
}
