using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;

/// <summary>Represents one ordered workflow-domain event in the Scylla read-model timeline.</summary>
public sealed record IntrinsicTimeStrategyWorkflowTimelineReadModel(
    StrategyWorkflowId WorkflowId,
    long EventId,
    string WorkflowEntityId,
    long WorkflowRevision,
    StrategyWorkflowStage Stage,
    string EventName,
    int EventSchemaVersion,
    ReadOnlyMemory<byte> EventPayload,
    DateTime OccurredAtUtc);
