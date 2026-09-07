using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>Contains the complete immutable workflow input supplied to the current strategy pipeline.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record IntrinsicTimeStrategyWorkflowView
{
    /// <summary>Gets the stable Strategy Workflow entity identity.</summary>
    [Key(0)] public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; } = new();
    /// <summary>Gets the accepted Strategy Workflow execution identity.</summary>
    [Key(1)] public StrategyWorkflowId WorkflowId { get; init; }
    /// <summary>Gets the original trigger event identity.</summary>
    [Key(2)] public Guid TriggerEventId { get; init; }
    /// <summary>Gets the workflow correlation identity.</summary>
    [Key(3)] public Guid CorrelationId { get; init; }
    /// <summary>Gets the command/event that caused the latest state transition.</summary>
    [Key(4)] public Guid CausationId { get; init; }
    /// <summary>Gets the immutable workflow-definition version.</summary>
    [Key(5)] public int WorkflowDefinitionVersion { get; init; }
    /// <summary>Gets the authoritative atomic machine status.</summary>
    [Key(6)] public WorkflowStrategyMachineStatus Status { get; init; }
    /// <summary>Gets the pipeline stage currently owned by orchestration.</summary>
    [Key(7)] public StrategyWorkflowStage CurrentStage { get; init; }
    /// <summary>Gets the revision of this complete view.</summary>
    [Key(8)] public long WorkflowRevision { get; init; }
    /// <summary>Gets when this workflow execution started.</summary>
    [Key(9)] public DateTime StartedAtUtc { get; init; }
    /// <summary>Gets when this complete view was committed.</summary>
    [Key(10)] public DateTime UpdatedAtUtc { get; init; }
    /// <summary>Gets the fixed maximum UTC completion deadline.</summary>
    [Key(11)] public DateTime ExpiresAtUtc { get; init; }
    /// <summary>Gets when this workflow became terminal, when applicable.</summary>
    [Key(12)] public DateTime? TerminalAtUtc { get; init; }
    /// <summary>Gets the immutable Regime Discovery pipeline view.</summary>
    [Key(13)] public StrategyWorkflowStageState RegimeDiscovery { get; init; } = new();
    /// <summary>Gets the immutable Market Condition pipeline view.</summary>
    [Key(14)] public StrategyWorkflowStageState MarketCondition { get; init; } = new();
    /// <summary>Gets the immutable Trade Selection pipeline view.</summary>
    [Key(15)] public StrategyWorkflowStageState TradeSelection { get; init; } = new();
    /// <summary>Gets the immutable Order Composition pipeline view.</summary>
    [Key(16)] public StrategyWorkflowStageState OrderComposition { get; init; } = new();
    /// <summary>Gets the immutable Risk Management pipeline view.</summary>
    [Key(17)] public StrategyWorkflowStageState RiskManagement { get; init; } = new();
    /// <summary>Gets the stable reason explaining a terminal failure, timeout, or cancellation.</summary>
    [Key(18)] public string StopReasonCode { get; init; } = string.Empty;
    /// <summary>Gets the frozen Regime Discovery parameter set selected for this workflow.</summary>
    [Key(19)] public RegimeDiscoveryParameterSet RegimeDiscoveryParameterSet { get; init; } = new();
    /// <summary>Gets the canonical hash of the frozen Regime Discovery parameter payload.</summary>
    [Key(20)] public string RegimeDiscoveryParameterPayloadSha256 { get; init; } = string.Empty;
    /// <summary>Gets the complete immutable trigger supplied to pipeline actors.</summary>
    [Key(21)] public FuturesItiSignalGeneratedEvent TriggerEvent { get; init; } = new();
    /// <summary>Gets the explicit terminal business outcome.</summary>
    [Key(22)] public StrategyWorkflowOutcome Outcome { get; init; }
    /// <summary>Gets the fund identity frozen for Market Condition.</summary>
    [Key(23)] public int FundId { get; init; }
    /// <summary>Gets the frozen Market Condition parameters.</summary>
    [Key(24)] public MarketConditionParameterSet MarketConditionParameterSet { get; init; } = new();
    /// <summary>Gets the canonical frozen Market Condition parameter hash.</summary>
    [Key(25)] public string MarketConditionParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(26)] public MarketConditionAssessmentBinding? AssessmentBinding { get; init; }
}
