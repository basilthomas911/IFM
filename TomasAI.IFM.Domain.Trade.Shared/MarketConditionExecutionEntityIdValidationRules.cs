using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared;

public sealed class MarketConditionExecutionEntityIdValidationRules
    : IValidationStructRules<MarketConditionExecutionEntityId>
{
    public ValidationError[] Execute(MarketConditionExecutionEntityId entityId)
        => new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(entityId.WorkflowEntityId)
            .Concat(new StrategyWorkflowIdValidationRules().Execute(entityId.WorkflowId))
            .ToArray();
}
