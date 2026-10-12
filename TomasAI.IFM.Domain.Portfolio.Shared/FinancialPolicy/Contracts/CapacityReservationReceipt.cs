using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: CapacityReservationReceipt; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record CapacityReservationReceipt
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid OperationId { get; init; }
    [Key(2)] public Guid ReservationId { get; init; }
    [Key(3)] public int PortfolioId { get; init; }
    [Key(4)] public int FundId { get; init; }
    [Key(5)] public int BookId { get; init; }
    [Key(6)] public int OrderId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(7)] public int[] TradeIds { get; init; } = [];
    [Key(8)] public Guid RiskResultId { get; init; }
    [Key(9)] public string RiskAssessmentHash { get; init; } = string.Empty;
    [Key(10)] public string CompositionResultHash { get; init; } = string.Empty;
    [Key(11)] public string UnitCandidateHash { get; init; } = string.Empty;
    [Key(12)] public string SizedOrderHash { get; init; } = string.Empty;
    [Key(13)] public int StrategyUnits { get; init; }
    [Key(14)] public CapacityRequirements Requirements { get; init; } = new();
    [Key(15)] public long AuthorityEpoch { get; init; }
    [Key(16)] public long FinancialRevision { get; init; }
    [Key(17)] public DateTime GrantedAtUtc { get; init; }
    [Key(18)] public DateTime ValidUntilUtc { get; init; }
    [Key(19)] public string ExecutionEnvironment { get; init; } = string.Empty;
    [Key(20)] public Guid CompletedEventId { get; init; }
    [Key(21)] public string InputHash { get; init; } = string.Empty;
}
