using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>
/// Validation rules for <see cref="FuturesRsiDailySignalEntityId"/>.
/// </summary>
public class FuturesRsiDailySignalEntityIdValidationRules : BaseValidationRules, IValidationRules<FuturesRsiDailySignalEntityId>
{
    public const string InstanceErrorMessage = "FuturesRsiSignalEntityId instance is null";
    public const string ContractIdErrorMessage = "FuturesRsiSignalEntityId: ContractId is required";
    public const string TimePeriodErrorMessage = "FuturesRsiSignalEntityId: TimePeriod must be a valid value other than None";

    static readonly FuturesRsiDailySignalEntityIdValidator Validator = new();

    public ValidationError[] Execute(FuturesRsiDailySignalEntityId rsiSignalEntityId) => Validate(rsiSignalEntityId, Validator);

    sealed class FuturesRsiDailySignalEntityIdValidator : AbstractValidator<FuturesRsiDailySignalEntityId>
    {
        public FuturesRsiDailySignalEntityIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.PeriodLength).GreaterThan(0).WithMessage("FuturesRsiSignalEntityId: PeriodLength must be a positive integer");
            RuleFor(x => x.TimePeriod).IsInEnum().NotEqual(TimeFrameType.None).WithMessage(TimePeriodErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Return aggregated validation failures for malformed external models rather than allowing exceptions to escape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The validation diagnostic intentionally identifies the validated model property.")]
        public override ValidationResult Validate(ValidationContext<FuturesRsiDailySignalEntityId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesRsiDailySignalEntityId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}

