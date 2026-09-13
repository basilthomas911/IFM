using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

[MessagePackObject]
public readonly record struct MarketConditionAssessmentExecutionId(
    [property: Key(0)] IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId,
    [property: Key(1)] StrategyWorkflowId WorkflowId) : IActorEntityId
{
    public string Format() => $"{WorkflowEntityId.Format()}.MarketCondition.AssessmentV2.{WorkflowId}";
}
