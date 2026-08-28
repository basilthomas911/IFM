using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

/// <summary>Identifies one Regime Discovery execution within one accepted strategy workflow.</summary>
/// <remarks>
/// The stable workflow entity remains the orchestration boundary. Adding the workflow execution identity creates a
/// private pipeline stream that cannot collide with an earlier or later workflow for the same strategy entity.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public readonly record struct RegimeDiscoveryExecutionEntityId : IActorEntityId
{
    /// <summary>Gets the owning Strategy Workflow entity.</summary>
    [Key(0)]
    public IntrinsicTimeStrategyWorkflowEntityId WorkflowEntityId { get; init; }

    /// <summary>Gets the owning Strategy Workflow execution.</summary>
    [Key(1)]
    public StrategyWorkflowId WorkflowId { get; init; }

    /// <summary>Initializes an empty identity for serialization.</summary>
    public RegimeDiscoveryExecutionEntityId()
    {
        WorkflowEntityId = new IntrinsicTimeStrategyWorkflowEntityId();
        WorkflowId = default;
    }

    /// <summary>Initializes a composite Regime Discovery execution identity.</summary>
    [SerializationConstructor]
    public RegimeDiscoveryExecutionEntityId(
        IntrinsicTimeStrategyWorkflowEntityId workflowEntityId,
        StrategyWorkflowId workflowId)
    {
        WorkflowEntityId = workflowEntityId;
        WorkflowId = workflowId;
    }

    /// <summary>Creates a composite execution identity from an accepted workflow.</summary>
    public static RegimeDiscoveryExecutionEntityId Create(
        IntrinsicTimeStrategyWorkflowEntityId workflowEntityId,
        StrategyWorkflowId workflowId)
        => new(workflowEntityId, workflowId);

    /// <summary>Formats the stable actor-routing and private stream identity.</summary>
    public string Format() => $"{WorkflowEntityId.Format()}.RegimeDiscovery.{WorkflowId}";

    /// <inheritdoc />
    public override string ToString() => Format();
}

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
