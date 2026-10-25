using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Provides validation rules for <see cref="FuturesTdiSignalId"/> instances using FluentValidation.
/// </summary>
/// <remarks>
/// <para>
/// This class defines validation constraints to ensure that a <see cref="FuturesTdiSignalId"/> is properly
/// constructed with valid data before being used in domain operations or persistence scenarios.
/// </para>
/// <para>
/// Validation rules include:
/// <list type="bullet">
/// <item><description>ContractId must not be null or empty</description></item>
/// <item><description>ValueDate must be greater than DateOnly.MinValue</description></item>
/// <item><description>ValueDate must be less than DateOnly.MaxValue</description></item>
/// <item><description>Timestamp must be greater than TimeOnly.MinValue</description></item>
/// <item><description>Timestamp must be less than TimeOnly.MaxValue</description></item>
/// </list>
/// </para>
/// <para>
/// Use the <see cref="Execute"/> method to validate an instance and retrieve any validation errors.
/// </para>
/// </remarks>
public class FuturesTdiSignalIdValidationRules : BaseValidationRules, IValidationRules<FuturesTdiSignalId>
{
    public const string InstanceErrorMessage = "FuturesTdiSignalId instance is null";
    public const string ContractIdErrorMessage = "FuturesTdiSignalId: ContractId is required";
    public const string ValueDateMinErrorMessage = "FuturesTdiSignalId: ValueDate must be greater than DateOnly.MinValue";
    public const string ValueDateMaxErrorMessage = "FuturesTdiSignalId: ValueDate must be less than DateOnly.MaxValue";
    public const string TimestampMinErrorMessage = "FuturesTdiSignalId: Timestamp must be greater than TimeOnly.MinValue";
    public const string TimestampMaxErrorMessage = "FuturesTdiSignalId: Timestamp must be less than TimeOnly.MaxValue";

    /// <summary>
    /// Validates the specified <see cref="FuturesTdiSignalId"/> instance against defined rules.
    /// </summary>
    /// <returns>
    /// An array of <see cref="ValidationError"/> instances describing any validation failures.
    /// Returns an empty array if validation succeeds.
    /// </returns>
    static readonly FuturesTdiSignalIdValidator Validator = new();

    public ValidationError[] Execute(FuturesTdiSignalId tdiSignalId) => Validate(tdiSignalId, Validator);

    sealed class FuturesTdiSignalIdValidator : AbstractValidator<FuturesTdiSignalId>
    {
        public FuturesTdiSignalIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MinValue).WithMessage(ValueDateMinErrorMessage);
            RuleFor(x => x.ValueDate).NotEqual(DateOnly.MaxValue).WithMessage(ValueDateMaxErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MinValue).WithMessage(TimestampMinErrorMessage);
            RuleFor(x => x.Timestamp).NotEqual(TimeOnly.MaxValue).WithMessage(TimestampMaxErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Return aggregated validation failures for malformed external models rather than allowing exceptions to escape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The validation diagnostic intentionally identifies the validated model property.")]
        public override ValidationResult Validate(ValidationContext<FuturesTdiSignalId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesTdiSignalId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
