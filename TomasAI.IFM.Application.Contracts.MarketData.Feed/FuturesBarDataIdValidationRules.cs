using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesBarDataId"/> instances.
/// </summary>
/// <remarks>
/// Validates FuturesBarDataId ensuring all required fields are present and valid.
/// </remarks>
public class FuturesBarDataIdValidationRules : BaseValidationRules, IValidationRules<FuturesBarDataId>
{
    /// <summary>
    /// Executes validation rules against the specified FuturesBarDataId instance.
    /// </summary>
    /// <param name="futuresBarDataId">The FuturesBarDataId to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesBarDataId futuresBarDataId)
        => Validate(futuresBarDataId, new FuturesBarDataIdValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesBarDataId.
    /// </summary>
    private sealed class FuturesBarDataIdValidator : AbstractValidator<FuturesBarDataId>
    {
        public FuturesBarDataIdValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage("FuturesBarDataId.ContractId is required");

            // Symbol validation
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("FuturesBarDataId.Symbol is required");

            // ValueDate validation
            RuleFor(x => x.ValueDate)
                .Must(valueDate => valueDate != DateOnly.MinValue && valueDate != DateOnly.MaxValue)
                .WithMessage("FuturesBarDataId.ValueDate is invalid");
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<FuturesBarDataId> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.InstanceToValidate is null)
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesBarDataId", "FuturesBarDataId instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
