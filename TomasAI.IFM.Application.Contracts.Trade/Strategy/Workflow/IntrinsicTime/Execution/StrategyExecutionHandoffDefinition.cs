using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;

/// <summary>Complete recoverable opening intent, saved requests and authoritative execution evidence.</summary>
[MessagePackObject]
public sealed record StrategyExecutionHandoffDefinition
{
    /// <summary>Stable execution identity reused for every delivery and reconciliation.</summary>
    [Key(0)] public OrderExecutionId Id { get; init; }
    /// <summary>Verified reference to the committed authorizing workflow and its content hash.</summary>
    [Key(1)] public StrategyOpeningAuthorization Authorization { get; init; } = new();
    /// <summary>Exact broker-neutral opening order frozen before external effects.</summary>
    [Key(2)] public TradeOrderDefinition Order { get; init; } = new();
    /// <summary>Canonical fingerprint checked on duplicate acceptance.</summary>
    [Key(3)] public string IntentHash { get; init; } = string.Empty;
    /// <summary>Monotonically increasing state revision.</summary>
    [Key(4)] public long Revision { get; init; }
    /// <summary>Last committed execution-admission checkpoint.</summary>
    [Key(5)] public StrategyExecutionHandoffPhase Phase { get; init; }
    /// <summary>Original legacy capacity request, when required by that financial authority.</summary>
    [Key(6)] public ConsumeCapacityReservationCommand? ConsumptionRequest { get; init; }
    /// <summary>Matching durable consumption receipt; never inferred from a timeout.</summary>
    [Key(7)] public CapacityConsumptionCompletedEvent? Consumption { get; init; }
    /// <summary>Latest observed broker execution evidence.</summary>
    [Key(8)] public OrderExecutionDefinition? Execution { get; init; }
    /// <summary>Visible recovery reason while the current operation remains unresolved.</summary>
    [Key(9)] public string ReconciliationReason { get; init; } = string.Empty;
    /// <summary>Server clock time of the last committed transition.</summary>
    [Key(10)] public DateTime UpdatedAtUtc { get; init; }
    /// <summary>Committed accounting and capacity evidence; authorization alone cannot populate it.</summary>
    [Key(11)] public LedgerPostingBatchCompletedEvent? FinancialReceipt { get; init; }
    /// <summary>Observed Fund lifecycle after the same execution is applied.</summary>
    [Key(12)] public TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderProjectionReadModel? FundEvidence { get; init; }
    /// <summary>Frozen binding request time retained unchanged after a missing execution reply.</summary>
    [Key(13)] public DateTime? ExecutionRequestedAtUtc { get; init; }
}
