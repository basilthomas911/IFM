using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Command.Validation;

public class EconomicCalendarValidationRules : BaseValidationRules, IValidationRules<EconomicCalendarReadModel>
{
    public const string InstanceErrorMessage = "EconomicCalendar instance is null";
    public const string EventDateErrorMessage = "EconomicCalendar.EventDate is required";
    public const string CountryCodeErrorMessage = "EconomicCalendar.CountryCode is empty";
    public const string EventNameErrorMessage = "EconomicCalendar.EventName is empty";

    public ValidationError[] Execute(EconomicCalendarReadModel economicCalendar) => Validate(economicCalendar, new EconomicCalendarValidator());

    class EconomicCalendarValidator : AbstractValidator<EconomicCalendarReadModel>
    {
        public EconomicCalendarValidator()
        {
            RuleFor(x => x.EventDate).Must(e => e != DateTime.MinValue && e != DateTime.MaxValue).WithMessage(EventDateErrorMessage);
            RuleFor(x => x.CountryCode).NotEmpty().WithMessage(CountryCodeErrorMessage);
            RuleFor(x => x.EventName).NotEmpty().WithMessage(EventNameErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<EconomicCalendarReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("EconomicCalendar", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
