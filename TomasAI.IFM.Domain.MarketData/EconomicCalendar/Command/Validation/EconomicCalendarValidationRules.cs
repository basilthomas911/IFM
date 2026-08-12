using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.Validation;

public class EconomicCalendarValidationRules : BaseValidationRules, IValidationRules<EconomicCalendarReadModel>
{
    static readonly EconomicCalendarValidator Validator = new();
    public const string InstanceErrorMessage = "EconomicCalendar instance is null";
    public const string EventDateErrorMessage = "EconomicCalendar.EventDate is required";
    public const string CountryCodeErrorMessage = "EconomicCalendar.CountryCode is empty";
    public const string EventNameErrorMessage = "EconomicCalendar.EventName is empty";

    public ValidationError[] Execute(EconomicCalendarReadModel economicCalendar) => Validate(economicCalendar, Validator);

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
