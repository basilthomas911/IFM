using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Represents the authoritative outcome of one workflow start request.</summary>
public sealed record IntrinsicTimeStrategyWorkflowStartAttemptReadModel(
    string WorkflowEntityId,
    DateTime RequestedAtUtc,
    StrategyWorkflowId RequestedWorkflowId,
    StrategyWorkflowStartDecision Decision,
    StrategyWorkflowId? ActiveWorkflowId,
    Guid StartCommandId,
    Guid TriggerEventId,
    StrategyWorkflowStage ActiveStage,
    string ReasonCode,
    long SourceEventId);
