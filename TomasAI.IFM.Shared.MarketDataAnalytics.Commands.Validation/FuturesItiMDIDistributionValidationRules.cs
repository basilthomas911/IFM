using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands.Validation;

public class FuturesItiMDIDistributionValidationRules : BaseValidationRules, IValidationRules<FuturesItiMDIDistributionReadModel>
{
    public static string UpTrendMeanErrorMsg => "UpTrendMean must be a finite number";
    public static string UpTrendStdDevErrorMsg => "UpTrendStdDev must be non-negative and finite";
    public static string DownTrendMeanErrorMsg => "DownTrendMean must be a finite number";
    public static string DownTrendStdDevErrorMsg => "DownTrendStdDev must be non-negative and finite";
    public static string UpTrendingLimitErrorMsg => "UpTrendingLimit must be between 1 and 99 and finite";
    public static string DownTrendingLimitErrorMsg => "DownTrendingLimit must be between 1 and 99 and finite";

    public ValidationError[] Execute(FuturesItiMDIDistributionReadModel model)
        => Validate(model, new FuturesItiMDIDistributionValidator());

    class FuturesItiMDIDistributionValidator : AbstractValidator<FuturesItiMDIDistributionReadModel>
    {
        public FuturesItiMDIDistributionValidator()
        {
            RuleFor(x => x.UpTrendMean)
                .Must(x => !double.IsNaN(x) && !double.IsInfinity(x))
                .WithMessage(UpTrendMeanErrorMsg);
            RuleFor(x => x.UpTrendStdDev)
                .GreaterThanOrEqualTo(0)
                .Must(x => !double.IsNaN(x) && !double.IsInfinity(x))
                .WithMessage(UpTrendStdDevErrorMsg);
            RuleFor(x => x.DownTrendMean)
                .Must(x => !double.IsNaN(x) && !double.IsInfinity(x))
                .WithMessage(DownTrendMeanErrorMsg);
            RuleFor(x => x.DownTrendStdDev)
                .GreaterThanOrEqualTo(0)
                .Must(x => !double.IsNaN(x) && !double.IsInfinity(x))
                .WithMessage(DownTrendStdDevErrorMsg);
            RuleFor(x => x.UpTrendingLimit)
                .InclusiveBetween(1, 99)
                .Must(x => !double.IsNaN(x) && !double.IsInfinity(x))
                .WithMessage(UpTrendingLimitErrorMsg);
            RuleFor(x => x.DownTrendingLimit)
                .InclusiveBetween(1, 99)
                .Must(x => !double.IsNaN(x) && !double.IsInfinity(x))
                .WithMessage(DownTrendingLimitErrorMsg);
        }

        public override ValidationResult Validate(ValidationContext<FuturesItiMDIDistributionReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesItiMDIDistributionReadModel", "Instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
