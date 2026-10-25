using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesDataId"/> instances.
/// </summary>
/// <remarks>
/// Validates futures data identifier ensuring all required fields are present and valid.
/// </remarks>
public class FuturesDataIdValidationRules : BaseValidationRules, IValidationRules<FuturesDataId>
{
    public const string InstanceErrorMessage = "FuturesDataId instance is null";
    public const string ContractIdErrorMessage = "FuturesDataId.ContractId is required";
    public const string ValueDateErrorMessage = "FuturesDataId.ValueDate is invalid";

    /// <summary>
    /// Executes validation rules against the specified FuturesDataId instance.
    /// </summary>
    /// <param name="futuresDataId">The futures data identifier to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesDataId futuresDataId)
        => Validate(futuresDataId, new FuturesDataIdValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesDataId.
    /// </summary>
    sealed class FuturesDataIdValidator : AbstractValidator<FuturesDataId>
    {
        public FuturesDataIdValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage(ContractIdErrorMessage);

            // ValueDate validation - must not be min or max value
            RuleFor(x => x.ValueDate)
                .Must(valueDate => valueDate != DateOnly.MinValue && valueDate != DateOnly.MaxValue)
                .WithMessage(ValueDateErrorMessage);
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
        public override ValidationResult Validate(ValidationContext<FuturesDataId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesDataId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
