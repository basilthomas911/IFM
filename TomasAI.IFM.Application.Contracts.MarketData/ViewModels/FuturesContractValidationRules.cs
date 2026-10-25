using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesContractV3ReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates futures contract read model ensuring all required fields are present, valid, and consistent
/// with business rules for futures contracts.
/// </remarks>
public class FuturesContractValidationRules : BaseValidationRules, IValidationRules<FuturesContractV3ReadModel>
{
    /// <summary>
    /// Executes validation rules against the specified FuturesContractV3ReadModel instance.
    /// </summary>
    /// <param name="futuresContract">The futures contract read model to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesContractV3ReadModel futuresContract)
        => Validate(futuresContract, new FuturesContractV3ReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesContractV3ReadModel.
    /// </summary>
    private sealed class FuturesContractV3ReadModelValidator : AbstractValidator<FuturesContractV3ReadModel>
    {
        public FuturesContractV3ReadModelValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage("FuturesContract.ContractId is required");

            // Symbol validation
            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("FuturesContract.Symbol is required");

            // LocalSymbol validation
            RuleFor(x => x.LocalSymbol)
                .NotEmpty()
                .WithMessage("FuturesContract.LocalSymbol is required");

            // SecurityType validation - must be "FUT"
            RuleFor(x => x.SecurityType)
                .NotEmpty()
                .Equal("FUT")
                .WithMessage("FuturesContract.SecurityType must be 'FUT'");

            // Currency validation
            RuleFor(x => x.Currency)
                .NotEmpty()
                .WithMessage("FuturesContract.Currency is required");

            // Exchange validation
            RuleFor(x => x.Exchange)
                .NotEmpty()
                .WithMessage("FuturesContract.Exchange is required");

            // Multiplier validation
            RuleFor(x => x.Multiplier)
                .NotEmpty()
                .WithMessage("FuturesContract.Multiplier is required");

            // LastTradeDate validation - must not be min or max value
            RuleFor(x => x.LastTradeDate)
                .Must(lastTradeDate => lastTradeDate != DateOnly.MinValue && lastTradeDate != DateOnly.MaxValue)
                .WithMessage("FuturesContract.LastTradeDate is invalid");

            RuleFor(x => x.Rollover)
                .Equal(true)
                .When(x => x.OnTheRun)
                .WithMessage("An on-the-run futures contract must belong to the rollover set");

            // Description validation (optional but if provided should not be whitespace only)
            RuleFor(x => x.Description)
                .Must(description => description == null || !string.IsNullOrWhiteSpace(description))
                .WithMessage("FuturesContract.Description must not be whitespace only");
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
        public override ValidationResult Validate(ValidationContext<FuturesContractV3ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesContract", "FuturesContract instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
