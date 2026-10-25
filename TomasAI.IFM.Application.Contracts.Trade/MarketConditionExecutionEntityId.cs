using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one Market Condition execution within one accepted strategy workflow.</summary>
[MessagePackObject(AllowPrivate = true)]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct MarketConditionExecutionEntityId : IActorEntityId
{
    [Key(0)] public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId { get; init; }
    [Key(1)] public StrategyWorkflowId WorkflowId { get; init; }

    public MarketConditionExecutionEntityId()
    {
        WorkflowEntityId = new IntrinsicTimeStrategyWorkflowEntityId();
        WorkflowId = default;
    }

    [SerializationConstructor]
    public MarketConditionExecutionEntityId(
        IntrinsicTimeStrategyWorkflowEntityId workflowEntityId,
        StrategyWorkflowId workflowId)
    {
        WorkflowEntityId = workflowEntityId;
        WorkflowId = workflowId;
    }

    public static MarketConditionExecutionEntityId Create(
        IntrinsicTimeStrategyWorkflowEntityId workflowEntityId,
        StrategyWorkflowId workflowId) => new(workflowEntityId, workflowId);

    public string Format() => $"{WorkflowEntityId.Format()}.MarketCondition.{WorkflowId}";
    public override string ToString() => Format();
}
