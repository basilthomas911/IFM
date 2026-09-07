using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
[MessagePackObject]
public readonly record struct TradeSelectionExecutionId(
    [property:Key(0)] IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId,
    [property:Key(1)] StrategyWorkflowId WorkflowId,
    [property:Key(2)] long InputWorkflowRevision):IActorEntityId
{
    public string Format() => $"{WorkflowEntityId.Format()}.TradeSelection.V1.{WorkflowId}.{InputWorkflowRevision}";
}
