using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Reference;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Validation;

public class EconomicCalendarIdValidationRules : BaseValidationRules, IValidationRules<EconomicCalendarId>
{
    public const string InstanceErrorMessage = "EconomicCalendarId instance is null";
    public const string EventDateErrorMessage = "EconomicCalendarId.EventDate is required";
    public const string CountryCodeErrorMessage = "EconomicCalendarId.CountryCode is empty";
    public const string EventNameErrorMessage = "EconomicCalendarId.EventName is empty";

    public ValidationError[] Execute(EconomicCalendarId economicCalendarId) => Validate(economicCalendarId, new EconomicCalendarIdValidator());

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

        public override ValidationResult Validate(ValidationContext<EconomicCalendarId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("EconomicCalendarId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
