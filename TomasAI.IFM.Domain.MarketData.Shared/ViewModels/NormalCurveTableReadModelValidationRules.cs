using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Provides FluentValidation rules for <see cref="NormalCurveTableReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates normal curve table ensuring the table array is not null or empty.
/// </remarks>
public class NormalCurveTableReadModelValidationRules : BaseValidationRules, IValidationRules<NormalCurveTableReadModel>
{
    public const string InstanceErrorMessage = "NormalCurveTableReadModel instance is null";
    public const string NormalCurveTableErrorMessage = "NormalCurveTableReadModel.NormalCurveTable is required and must not be empty";

    /// <summary>
    /// Executes validation rules against the specified NormalCurveTableReadModel instance.
    /// </summary>
    /// <param name="normalCurveTable">The normal curve table to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(NormalCurveTableReadModel normalCurveTable)
        => Validate(normalCurveTable, new NormalCurveTableReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for NormalCurveTableReadModel.
    /// </summary>
    sealed class NormalCurveTableReadModelValidator : AbstractValidator<NormalCurveTableReadModel>
    {
        public NormalCurveTableReadModelValidator()
        {
            // NormalCurveTable validation
            RuleFor(x => x.NormalCurveTable)
                .NotNull()
                .NotEmpty()
                .WithMessage(NormalCurveTableErrorMessage);
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
        public override ValidationResult Validate(ValidationContext<NormalCurveTableReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("NormalCurveTableReadModel", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}