using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Implemented by the committed Risk result, never by a capacity request supplied by a caller.</summary>
public interface ICapacityAssessmentCompletedEvent : IEvent
{
    QualifiedCapacityAssessment CapacityAssessment { get; }
}

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

/// <summary>Implemented by the durable workflow acceptance event that precedes capacity consumption.</summary>
public interface ICapacityExecutionAcceptedEvent : IEvent
{
    CapacityExecutionAcceptance CapacityAcceptance { get; }
}

/// <summary>Terminal execution reconciliation committed after all known financial facts are posted.</summary>
public interface ICapacityExecutionReconciledEvent : IEvent
{
    CapacityExecutionReconciliation CapacityReconciliation { get; }
}

[MessagePackObject]
public sealed record CapacityExecutionReconciliation
{
    [Key(0)] public Guid ExecutionId { get; init; }
    [Key(1)] public long ExecutionRevision { get; init; }
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int FundId { get; init; }
    [Key(4)] public int OrderId { get; init; }
    [Key(5)] public Guid ReservationId { get; init; }
    [Key(6)] public string SourceContentHash { get; init; }=string.Empty;
    [Key(7)] public int FilledUnits { get; init; }
    [Key(8)] public int CancelledUnits { get; init; }
    [Key(9)] public bool FinancialFactsComplete { get; init; }
    [Key(10)] public string ReconciliationReference { get; init; }=string.Empty;
    [Key(11)] public string Environment { get; init; }=string.Empty;
    [Key(12)] public int ClosedUnits { get; init; }
}

[MessagePackObject]
public sealed record CapacityExecutionAcceptance
{
    [Key(0)] public Guid ExecutionId { get; init; }
    [Key(1)] public long ExecutionRevision { get; init; }
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int FundId { get; init; }
    [Key(4)] public int OrderId { get; init; }
    [Key(5)] public Guid ReservationId { get; init; }
    [Key(6)] public string SizedOrderHash { get; init; } = string.Empty;
    [Key(7)] public string RequirementsHash { get; init; } = string.Empty;
    [Key(8)] public string Environment { get; init; } = string.Empty;
    [Key(9)] public DateTime ValidUntilUtc { get; init; }
    [Key(10)] public string ExecutionOrderHash { get; init; } = "";
}
