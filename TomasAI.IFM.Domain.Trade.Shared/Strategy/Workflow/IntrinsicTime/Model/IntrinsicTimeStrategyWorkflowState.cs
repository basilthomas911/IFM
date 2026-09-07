using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

/// <summary>
/// Represents the immutable public snapshot owned by one Intrinsic Time Strategy workflow execution.
/// </summary>
/// <remarks>
/// The snapshot contains only workflow-owned state and accepted opaque stage results. Pipeline-private state and the
/// original ITI trigger event are deliberately excluded.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public sealed record IntrinsicTimeStrategyWorkflowState
{
    /// <summary>Gets the stable workflow actor entity identity.</summary>
    [Key(0)]
    public IntrinsicTimeStrategyWorkflowEntityId EntityId { get; init; } = new();

    /// <summary>Gets the unique identity of this accepted workflow execution.</summary>
    [Key(1)]
    public StrategyWorkflowId WorkflowId { get; init; }

    /// <summary>Gets the identity of the ITI signal event that proposed this execution.</summary>
    [Key(2)]
    public Guid TriggerEventId { get; init; }

    /// <summary>Gets the correlation identity shared by the workflow execution.</summary>
    [Key(3)]
    public Guid CorrelationId { get; init; }

    /// <summary>Gets the workflow-definition version used by this execution.</summary>
    [Key(4)]
    public int WorkflowDefinitionVersion { get; init; }

    /// <summary>Gets the current workflow status.</summary>
    [Key(5)]
    public StrategyWorkflowStatus Status { get; init; }

    /// <summary>Gets the terminal outcome, or <see cref="StrategyWorkflowOutcome.None"/> while nonterminal.</summary>
    [Key(6)]
    public StrategyWorkflowOutcome Outcome { get; init; }

    /// <summary>Gets the stage currently owned by workflow orchestration.</summary>
    [Key(7)]
    public StrategyWorkflowStage CurrentStage { get; init; }

    /// <summary>Gets the revision produced by the latest accepted logical workflow transition.</summary>
    [Key(8)]
    public long WorkflowRevision { get; init; }

    /// <summary>Gets the UTC timestamp at which this workflow execution started.</summary>
    [Key(9)]
    public DateTime StartedAtUtc { get; init; }

    /// <summary>Gets the UTC timestamp at which this workflow execution became terminal.</summary>
    [Key(10)]
    public DateTime? TerminalAtUtc { get; init; }

    /// <summary>Gets the Regime Discovery stage state.</summary>
    [Key(11)]
    public StrategyWorkflowStageState RegimeDiscovery { get; init; } = new();

    /// <summary>Gets the Market Condition stage state.</summary>
    [Key(12)]
    public StrategyWorkflowStageState MarketCondition { get; init; } = new();

    /// <summary>Gets the Trade Selection stage state.</summary>
    [Key(13)]
    public StrategyWorkflowStageState TradeSelection { get; init; } = new();

    /// <summary>Gets the Order Composition stage state.</summary>
    [Key(14)]
    public StrategyWorkflowStageState OrderComposition { get; init; } = new();

    /// <summary>Gets the Risk Management stage state.</summary>
    [Key(15)]
    public StrategyWorkflowStageState RiskManagement { get; init; } = new();

    /// <summary>Gets the stable reason code explaining why workflow processing stopped.</summary>
    [Key(16)]
    public string StopReasonCode { get; init; } = string.Empty;

    /// <summary>Gets the immutable Regime Discovery parameters selected when this workflow was accepted.</summary>
    [Key(17)]
    public RegimeDiscoveryParameterSet RegimeDiscoveryParameterSet { get; init; } = new();

    /// <summary>Gets the canonical SHA-256 hash of the selected Regime Discovery parameter payload.</summary>
    [Key(18)]
    public string RegimeDiscoveryParameterPayloadSha256 { get; init; } = string.Empty;

    [Key(19)] public int FundId { get; init; }
    [Key(20)] public MarketConditionParameterSet MarketConditionParameterSet { get; init; } = new();
    [Key(21)] public string MarketConditionParameterPayloadSha256 { get; init; } = string.Empty;
    [Key(22)] public MarketConditionAssessmentBinding? AssessmentBinding { get; init; }
}
