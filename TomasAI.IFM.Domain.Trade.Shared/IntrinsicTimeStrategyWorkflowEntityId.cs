using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>
/// Identifies one Intrinsic Time Strategy workflow stream for a specific futures ITI timeframe entity.
/// </summary>
/// <remarks>
/// The workflow definition and complete futures ITI entity identity form the actor-routing boundary. Sequential
/// executions for this identity share one command stream.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public readonly record struct IntrinsicTimeStrategyWorkflowEntityId : IActorEntityId
{
    /// <summary>Gets the stable workflow-definition identifier.</summary>
    [Key(0)]
    public string WorkflowDefinitionId { get; init; }

    /// <summary>Gets the futures ITI timeframe identity that triggered the workflow.</summary>
    [Key(1)]
    public FuturesItiSignalEntityId ItiSignalEntityId { get; init; }

    /// <summary>Initializes an empty instance for serialization.</summary>
    public IntrinsicTimeStrategyWorkflowEntityId()
    {
        WorkflowDefinitionId = string.Empty;
        ItiSignalEntityId = new FuturesItiSignalEntityId();
    }

    /// <summary>Initializes a workflow entity identity.</summary>
    /// <param name="workflowDefinitionId">Stable workflow-definition identifier.</param>
    /// <param name="itiSignalEntityId">Futures ITI timeframe identity.</param>
    [SerializationConstructor]
    public IntrinsicTimeStrategyWorkflowEntityId(
        string workflowDefinitionId,
        FuturesItiSignalEntityId itiSignalEntityId)
    {
        WorkflowDefinitionId = workflowDefinitionId;
        ItiSignalEntityId = itiSignalEntityId;
    }

    /// <summary>Creates an identity for the current Intrinsic Time Strategy workflow definition.</summary>
    /// <param name="itiSignalEntityId">Futures ITI timeframe identity.</param>
    /// <returns>The workflow actor entity identity.</returns>
    public static IntrinsicTimeStrategyWorkflowEntityId Create(FuturesItiSignalEntityId itiSignalEntityId)
    {
        ArgumentNullException.ThrowIfNull(itiSignalEntityId);
        return new(IntrinsicTimeStrategyWorkflowDefinition.Id, itiSignalEntityId);
    }

    /// <summary>Formats the stable actor-routing identity.</summary>
    /// <returns>The workflow definition followed by the complete futures ITI identity.</returns>
    public string Format() => $"{WorkflowDefinitionId}.{ItiSignalEntityId?.Format()}";

    /// <summary>Returns the stable formatted actor-routing identity.</summary>
    public override string ToString() => Format();
}
