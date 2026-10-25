using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Validates that a strategy workflow execution identity is a non-empty UUIDv7 value.</summary>
public sealed class StrategyWorkflowIdValidationRules
    : BaseValidationRules, IValidationStructRules<StrategyWorkflowId>
{
    /// <summary>Error returned for an empty execution identity.</summary>
    public const string EmptyErrorMessage = "StrategyWorkflowId: Value is required";

    /// <summary>Error returned for a GUID that is not version 7.</summary>
    public const string VersionErrorMessage = "StrategyWorkflowId: Value must be UUIDv7";

    static readonly Validator Rules = new();

    /// <summary>Validates the supplied workflow execution identity.</summary>
    /// <param name="workflowId">Execution identity to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(StrategyWorkflowId workflowId) => Validate(workflowId, Rules);

    sealed class Validator : AbstractValidator<StrategyWorkflowId>
    {
        public Validator()
        {
            RuleFor(x => x.Value)
                .NotEmpty()
                .WithMessage(EmptyErrorMessage);
            RuleFor(x => x.Value)
                .Must(static value => value == Guid.Empty || value.Version == 7)
                .WithMessage(VersionErrorMessage);
        }
    }
}
