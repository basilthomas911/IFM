using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Plan.Validation;

public class TradePlanValidationRules 
    : BaseValidationRules, IValidationRules<TradePlanReadModel>
{
    public const string InstanceErrorMessage = "TradePlan instance is null";
    public const string TradePlanIdErrorMessage = "TradePlan.TradePlanId is empty";
    public const string OrderIdErrorMessage = "TradePlan.OrderId is < 1";
    public const string TradeIdErrorMessage = "TradePlan.TradeId is < 1";
    public const string TradeDateErrorMessage = "TradePlan.TradeDate is empty";
    public const string ValueDateErrorMessage = "TradePlan.ValueDate is empty";
    public const string MaturityDateErrorMessage = "TradePlan.MaturityDate is empty";
    public const string ActionDateErrorMessage = "TradePlan.ActionDate is empty";
    public const string ForwardLossRatioErrorMessage = "TradePlan.ForwardLossRatio is NaN";
    public const string LossProbabilityRatioErrorMessage = "TradePlan.LossProbability is NaN";
    public const string MScoreErrorMessage = "TradePlan.MScore is NaN";
    public const string AssetStdDevErrorMessage = "TradePlan.AssetStdDev is NaN";
    public const string AssetMeanErrorMessage = "TradePlan.AssetMean is NaN";
    public const string AssetPriceChangeErrorMessage = "TradePlan.AssetPriceChange is NaN";
    public const string PutOTMProbabilityErrorMessage = "TradePlan.PutOTMProbability is NaN";
    public const string CallOTMProbabilityErrorMessage = "TradePlan.CallOTMProbability is NaN";
    public const string ShortPutGammaErrorMessage = "TradePlan.ShortPutGamma is NaN";
    public const string ShortCallGammaErrorMessage = "TradePlan.ShortCallGamma is NaN";

    public ValidationError[] Execute(TradePlanReadModel tradePlan) => Validate(tradePlan, new TradePlanValidator());

    class TradePlanValidator : AbstractValidator<TradePlanReadModel>
    {
        public TradePlanValidator()
        {
            RuleFor(x => x.OrderId).Must(e => e > 0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).Must(e => e > 0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.TradeDate).NotEmpty().WithMessage(TradeDateErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.MaturityDate).NotEmpty().WithMessage(MaturityDateErrorMessage);
            RuleFor(x => x.ActionDate).NotEmpty().WithMessage(ActionDateErrorMessage);
            RuleFor(x => x.ForwardLossRatio).Must(e => !Double.IsNaN(e)).WithMessage(ForwardLossRatioErrorMessage);
            RuleFor(x => x.LossProbability).Must(e => !Double.IsNaN(e)).WithMessage(LossProbabilityRatioErrorMessage);
            RuleFor(x => x.MScore).Must(e => !Double.IsNaN(e)).WithMessage(MScoreErrorMessage);
            RuleFor(x => x.AssetStdDev).Must(e => !Double.IsNaN(e)).WithMessage(AssetStdDevErrorMessage);
            RuleFor(x => x.AssetMean).Must(e => !Double.IsNaN(e)).WithMessage(AssetMeanErrorMessage);
            RuleFor(x => x.AssetPriceChange).Must(e => !Double.IsNaN(e)).WithMessage(AssetPriceChangeErrorMessage);
            RuleFor(x => x.PutOTMProbability).Must(e => !Double.IsNaN(e)).WithMessage(PutOTMProbabilityErrorMessage);
            RuleFor(x => x.CallOTMProbability).Must(e => !Double.IsNaN(e)).WithMessage(CallOTMProbabilityErrorMessage);
            RuleFor(x => x.ShortPutGamma).Must(e => !Double.IsNaN(e)).WithMessage(ShortPutGammaErrorMessage);
            RuleFor(x => x.ShortCallGamma).Must(e => !Double.IsNaN(e)).WithMessage(ShortCallGammaErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<TradePlanReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("TradePlan", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
