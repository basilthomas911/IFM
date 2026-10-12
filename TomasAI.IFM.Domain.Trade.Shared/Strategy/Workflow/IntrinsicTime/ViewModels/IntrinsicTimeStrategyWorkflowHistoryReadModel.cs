using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Represents a workflow execution in an entity or status history query.</summary>
public sealed record IntrinsicTimeStrategyWorkflowHistoryReadModel(
    string WorkflowEntityId,
    DateTime StartedAtUtc,
    StrategyWorkflowId WorkflowId,
    StrategyWorkflowStatus Status,
    StrategyWorkflowOutcome Outcome,
    StrategyWorkflowStage CurrentStage,
    long WorkflowRevision,
    DateTime? TerminalAtUtc,
    string StopReasonCode);
