using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

/// <summary>Identifies one Market Condition execution within one accepted strategy workflow.</summary>
[MessagePackObject(AllowPrivate = true)]
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

public sealed class MarketConditionExecutionEntityIdValidationRules
    : IValidationStructRules<MarketConditionExecutionEntityId>
{
    public ValidationError[] Execute(MarketConditionExecutionEntityId entityId)
        => new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(entityId.WorkflowEntityId)
            .Concat(new StrategyWorkflowIdValidationRules().Execute(entityId.WorkflowId))
            .ToArray();
}
