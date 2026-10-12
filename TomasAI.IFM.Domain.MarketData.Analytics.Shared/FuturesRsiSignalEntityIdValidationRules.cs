using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Validation rules for <see cref="FuturesRsiSignalEntityId"/>.
/// </summary>
public class FuturesRsiSignalEntityIdValidationRules : BaseValidationRules, IValidationRules<FuturesRsiSignalEntityId>
{
    public const string InstanceErrorMessage = "FuturesRsiSignalEntityId instance is null";
    public const string ContractIdErrorMessage = "FuturesRsiSignalEntityId: ContractId is required";
    public const string ValueDateMinErrorMessage = "FuturesRsiSignalEntityId: ValueDate must be greater than DateOnly.MinValue";
    public const string ValueDateMaxErrorMessage = "FuturesRsiSignalEntityId: ValueDate must be less than DateOnly.MaxValue";
    public const string TimePeriodErrorMessage = "FuturesRsiSignalEntityId: TimePeriod must be a valid value other than None";

    static readonly FuturesRsiSignalEntityIdValidator Validator = new();

    public ValidationError[] Execute(FuturesRsiSignalEntityId rsiSignalEntityId) => Validate(rsiSignalEntityId, Validator);

    sealed class FuturesRsiSignalEntityIdValidator : AbstractValidator<FuturesRsiSignalEntityId>
    {
        public FuturesRsiSignalEntityIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.ValueDate).LessThan(DateOnly.MaxValue).WithMessage(ValueDateMaxErrorMessage);
            RuleFor(x => x.ValueDate).GreaterThan(DateOnly.MinValue).WithMessage(ValueDateMinErrorMessage);
            RuleFor(x => x.PeriodLength).GreaterThan(0).WithMessage("FuturesRsiSignalEntityId: PeriodLength must be a positive integer");
            RuleFor(x => x.TimePeriod).IsInEnum().NotEqual(TimeFrameType.None).WithMessage(TimePeriodErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Return aggregated validation failures for malformed external models rather than allowing exceptions to escape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The validation diagnostic intentionally identifies the validated model property.")]
        public override ValidationResult Validate(ValidationContext<FuturesRsiSignalEntityId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesRsiSignalEntityId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}

