using TomasAI.IFM.Domain.Trade.Shared;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>
/// Combines the authoritative workflow snapshot with the last Regime terminal projection without mutating either.
/// </summary>
[MessagePackObject]
public sealed record IntrinsicTimeStrategyWorkflowObservationReadModel
{
    [Key(0)] public string WorkflowEntityId { get; init; } = string.Empty;
    [Key(1)] public StrategyWorkflowId WorkflowId { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public WorkflowStrategyMachineStatus MachineStatus { get; init; }
    [Key(4)] public StrategyWorkflowStage CurrentStage { get; init; }
    [Key(5)] public long WorkflowRevision { get; init; }
    [Key(6)] public DateTime StartedAtUtc { get; init; }
    [Key(7)] public DateTime ExpiresAtUtc { get; init; }
    [Key(8)] public DateTime? TerminalAtUtc { get; init; }
    [Key(9)] public string StopReasonCode { get; init; } = string.Empty;
    [Key(10)] public IntrinsicTimeStrategyWorkflowOperationalStatus OperationalStatus { get; init; }
    [Key(11)] public bool IsOperationalIssue { get; init; }
    [Key(12)] public RegimeDiscoveryReadModel? RegimeTerminal { get; init; }
    [Key(13)] public bool WorkflowAcceptedRegimeTerminal { get; init; }
    [Key(14)] public bool NotificationLossSuspected { get; init; }
    [Key(15)] public DateTime ObservedAtUtc { get; init; }
    [Key(16)] public string Diagnostic { get; init; } = string.Empty;
    /// <summary>Gets the projected Market Condition terminal, including operational/UI evidence.</summary>
    [Key(17)] public MarketConditionReadModel? MarketConditionTerminal { get; init; }
    [Key(18)] public bool WorkflowAcceptedMarketConditionTerminal { get; init; }
    [Key(19)] public bool MarketConditionNotificationLossSuspected { get; init; }
    [Key(20)] public Pipeline.MarketCondition.Assessment.MarketConditionAssessmentResult? MarketAssessment { get; init; }
    [Key(21)] public bool WorkflowAcceptedMarketAssessment { get; init; }
    [Key(22)] public bool MarketAssessmentOrphanSuspected { get; init; }
    [Key(23)] public bool MarketAssessmentExpired { get; init; }
}
