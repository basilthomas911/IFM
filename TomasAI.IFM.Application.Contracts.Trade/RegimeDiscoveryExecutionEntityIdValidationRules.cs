using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Validates the workflow entity and workflow execution components of a Regime Discovery identity.</summary>
public sealed class RegimeDiscoveryExecutionEntityIdValidationRules
    : IValidationStructRules<RegimeDiscoveryExecutionEntityId>
{
    /// <inheritdoc />
    public ValidationError[] Execute(RegimeDiscoveryExecutionEntityId entityId)
        => new IntrinsicTimeStrategyWorkflowEntityIdValidationRules().Execute(entityId.WorkflowEntityId)
            .Concat(new StrategyWorkflowIdValidationRules().Execute(entityId.WorkflowId))
            .ToArray();
}
