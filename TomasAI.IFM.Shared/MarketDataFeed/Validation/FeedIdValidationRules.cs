using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed.Validation;

/// <summary>
/// Validation rules for <see cref="FeedId"/>.
/// </summary>
public class FeedIdValidationRules : BaseValidationRules, IValidationRules<FeedId>
{
    public const string InstanceErrorMessage = "FeedId instance is null";
    public const string ValueErrorMessage = "FeedId.Value must be greater than zero";

    public ValidationError[] Execute(FeedId feedId) => Validate(feedId, new FeedIdValidator());

    class FeedIdValidator : AbstractValidator<FeedId>
    {
        public FeedIdValidator()
        {
            RuleFor(x => x.Value).Must(e => e > 0).WithMessage(ValueErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<FeedId> context)
        {
            if (context.InstanceToValidate is null)
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FeedId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
