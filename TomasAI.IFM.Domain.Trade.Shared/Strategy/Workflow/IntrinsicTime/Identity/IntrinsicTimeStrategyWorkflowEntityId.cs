using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;

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
    public string Format() => $"{WorkflowDefinitionId}.{ItiSignalEntityId.Format()}";

    /// <summary>Returns the stable formatted actor-routing identity.</summary>
    public override string ToString() => Format();
}

/// <summary>Validates an <see cref="IntrinsicTimeStrategyWorkflowEntityId"/> before routing or persistence.</summary>
public sealed class IntrinsicTimeStrategyWorkflowEntityIdValidationRules
    : BaseValidationRules, IValidationStructRules<IntrinsicTimeStrategyWorkflowEntityId>
{
    /// <summary>Error returned when the workflow definition is unsupported.</summary>
    public const string WorkflowDefinitionErrorMessage =
        "IntrinsicTimeStrategyWorkflowEntityId: WorkflowDefinitionId must equal IntrinsicTimeStrategy";

    /// <summary>Error returned when the futures ITI identity is missing.</summary>
    public const string ItiSignalEntityErrorMessage =
        "IntrinsicTimeStrategyWorkflowEntityId: ItiSignalEntityId is required";

    /// <summary>Error returned when the futures contract identity is missing.</summary>
    public const string ContractIdErrorMessage =
        "IntrinsicTimeStrategyWorkflowEntityId: ContractId is required";

    /// <summary>Error returned when the timeframe start value date is invalid.</summary>
    public const string ValueDateErrorMessage =
        "IntrinsicTimeStrategyWorkflowEntityId: TimeFrameStartValueDate must be valid";

    /// <summary>Error returned when the ITI timeframe is not eligible for this workflow.</summary>
    public const string TimePeriodErrorMessage =
        "IntrinsicTimeStrategyWorkflowEntityId: TimePeriod must be Daily, Weekly, or Monthly";

    static readonly Validator Rules = new();

    /// <summary>Validates the supplied workflow entity identity.</summary>
    /// <param name="entityId">Workflow entity identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(IntrinsicTimeStrategyWorkflowEntityId entityId)
        => Validate(entityId, Rules);

    sealed class Validator : AbstractValidator<IntrinsicTimeStrategyWorkflowEntityId>
    {
        public Validator()
        {
            RuleFor(x => x.WorkflowDefinitionId)
                .Equal(IntrinsicTimeStrategyWorkflowDefinition.Id)
                .WithMessage(WorkflowDefinitionErrorMessage);

            RuleFor(x => x.ItiSignalEntityId)
                .NotNull()
                .WithMessage(ItiSignalEntityErrorMessage)
                .DependentRules(() =>
                {
                    RuleFor(x => x.ItiSignalEntityId.ContractId)
                        .NotEmpty()
                        .WithMessage(ContractIdErrorMessage);
                    RuleFor(x => x.ItiSignalEntityId.TimeFrameStartValueDate)
                        .Must(static valueDate => valueDate != DateOnly.MinValue && valueDate != DateOnly.MaxValue)
                        .WithMessage(ValueDateErrorMessage);
                    RuleFor(x => x.ItiSignalEntityId.TimePeriod)
                        .Must(IsEligibleTimePeriod)
                        .WithMessage(TimePeriodErrorMessage);
                });
        }

        static bool IsEligibleTimePeriod(TimeFrameType timePeriod)
            => timePeriod is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly;

    }
}
