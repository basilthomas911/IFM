using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands.Validation;

public class FuturesItiTrendCoastLineCountersValidationRules : BaseValidationRules, IValidationRules<FuturesItiTrendCoastLineCountersReadModel>
{
    public static string UpTrendCountErrorMsg => "UpTrendCount must be zero or positive";
    public static string DownTrendCountErrorMsg => "DownTrendCount must be zero or positive";

    public ValidationError[] Execute(FuturesItiTrendCoastLineCountersReadModel model)
        => Validate(model, new FuturesItiTrendCoastLineCountersValidator());

    class FuturesItiTrendCoastLineCountersValidator : AbstractValidator<FuturesItiTrendCoastLineCountersReadModel>
    {
        public FuturesItiTrendCoastLineCountersValidator()
        {
            RuleFor(x => x.UpTrendCount)
                .GreaterThanOrEqualTo(0)
                .WithMessage(UpTrendCountErrorMsg);
            RuleFor(x => x.DownTrendCount)
                .GreaterThanOrEqualTo(0)
                .WithMessage(DownTrendCountErrorMsg);
        }

        public override ValidationResult Validate(ValidationContext<FuturesItiTrendCoastLineCountersReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesItiTrendCoastLineCountersReadModel", "Instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
