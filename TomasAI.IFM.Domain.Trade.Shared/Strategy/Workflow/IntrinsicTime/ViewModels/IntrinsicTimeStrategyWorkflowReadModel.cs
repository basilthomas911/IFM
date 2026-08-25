using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Represents the current persisted read model for one Intrinsic Time Strategy workflow execution.</summary>
public sealed record IntrinsicTimeStrategyWorkflowReadModel(
    StrategyWorkflowId WorkflowId,
    string WorkflowEntityId,
    string WorkflowDefinitionId,
    int WorkflowDefinitionVersion,
    string ContractId,
    DateOnly TimeFrameStartValueDate,
    TimeFrameType TimePeriod,
    Guid TriggerEventId,
    Guid CorrelationId,
    StrategyWorkflowStatus Status,
    StrategyWorkflowOutcome Outcome,
    StrategyWorkflowStage CurrentStage,
    long WorkflowRevision,
    long LastEventId,
    int StateSchemaVersion,
    ReadOnlyMemory<byte> StatePayload,
    string StopReasonCode,
    DateTime StartedAtUtc,
    DateTime? TerminalAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>Represents the currently active workflow execution for one stable workflow entity.</summary>
public sealed record ActiveIntrinsicTimeStrategyWorkflowReadModel(
    string WorkflowEntityId,
    StrategyWorkflowId WorkflowId,
    string ContractId,
    DateOnly TimeFrameStartValueDate,
    TimeFrameType TimePeriod,
    StrategyWorkflowStage CurrentStage,
    long WorkflowRevision,
    long LastEventId,
    int StateSchemaVersion,
    ReadOnlyMemory<byte> StatePayload,
    DateTime StartedAtUtc,
    DateTime UpdatedAtUtc);

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
