using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared;

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
