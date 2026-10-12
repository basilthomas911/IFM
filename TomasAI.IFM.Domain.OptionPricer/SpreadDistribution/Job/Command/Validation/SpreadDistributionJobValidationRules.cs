using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Validation;

public class SpreadDistributionJobValidationRules : BaseValidationRules, IValidationRules<SpreadDistributionJobReadModel>
{
    static readonly SpreadDistributionJobValidator Validator = new();
    public const string InstanceErrorMessage = "SpreadDistributionJob cannot be null";
    public const string OrderIdErrorMessage = "SpreadDistributionJob.OrderId must be > 0";
    public const string TradeIdErrorMessage = "SpreadDistributionJob.TradeId must be > 0";
    public const string JobIdErrorMessage = "SpreadDistributionJob.JobId must be > 0";

    public ValidationError[] Execute(SpreadDistributionJobReadModel spreadDistributionJob) => Validate(spreadDistributionJob, Validator);

    class SpreadDistributionJobValidator : AbstractValidator<SpreadDistributionJobReadModel>
    {
        public SpreadDistributionJobValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
        }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")] 
        public override ValidationResult Validate(ValidationContext<SpreadDistributionJobReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.InstanceToValidate is null)
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("SpreadDistributionJob", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
