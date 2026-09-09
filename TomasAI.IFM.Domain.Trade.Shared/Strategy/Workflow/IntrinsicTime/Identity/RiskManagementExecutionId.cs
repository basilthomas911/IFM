using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

/// <summary>A retry with refreshed evidence is a new bounded attempt, never a mutation of a completed Function.</summary>
[MessagePackObject]
public readonly record struct RiskManagementExecutionId(
    [property: Key(0)] IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId,
    [property: Key(1)] StrategyWorkflowId WorkflowId,
    [property: Key(2)] long InputWorkflowRevision,
    [property: Key(3)] int AttemptOrdinal) : IActorEntityId
{
    public string Format() => $"{WorkflowEntityId.Format()}.RiskManagement.V1.{WorkflowId}.{InputWorkflowRevision}.{AttemptOrdinal}";
}
