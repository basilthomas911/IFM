using TomasAI.IFM.Domain.MarketData.Shared;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Validation;

public class EconomicCalendarIdValidationRules : BaseValidationRules, IValidationRules<EconomicCalendarId>
{
    static readonly EconomicCalendarIdValidator Validator = new();
    public const string InstanceErrorMessage = "EconomicCalendarId instance is null";
    public const string EventDateErrorMessage = "EconomicCalendarId.EventDate is required";
    public const string CountryCodeErrorMessage = "EconomicCalendarId.CountryCode is empty";
    public const string EventNameErrorMessage = "EconomicCalendarId.EventName is empty";

    public ValidationError[] Execute(EconomicCalendarId economicCalendarId) => Validate(economicCalendarId, Validator);

    class EconomicCalendarIdValidator : AbstractValidator<EconomicCalendarId>
    {
        public EconomicCalendarIdValidator()
        {
            RuleFor(x => x.EventDate)
                .Must(e => e != DateTime.MinValue && e != DateTime.MaxValue)
                .WithMessage(EventDateErrorMessage);

            RuleFor(x => x.CountryCode)
                .NotEmpty()
                .WithMessage(CountryCodeErrorMessage);

            RuleFor(x => x.EventName)
                .NotEmpty()
                .WithMessage(EventNameErrorMessage);
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")] 
        public override ValidationResult Validate(ValidationContext<EconomicCalendarId> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.InstanceToValidate is null)
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("EconomicCalendarId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
