using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Validation;

/// <summary>
/// Provides validation rules for the LookupTypeReadModel, ensuring that required fields are populated and values meet
/// defined constraints.
/// </summary>
/// <remarks>This class implements validation logic for LookupTypeReadModel instances, including checks for
/// non-empty fields and positive numeric values. It can be used to validate user input or data models before processing
/// or persistence. The class exposes standard error messages for common validation failures, which can be used for
/// consistent error reporting across the application.</remarks>
public class LookupTypeValidationRules : BaseValidationRules, IValidationRules<LookupTypeReadModel>
{
    public const string InstanceErrorMessage = "LookupType instance is null";
    public const string LookupTypeNameErrorMessage = "LookupTypeName is empty";
    public const string ShortCodeErrorMessage = "ShortCode is empty";
    public const string OrderIdErrorMessage = "OrderId must be positive number";

    public ValidationError[] Execute(LookupTypeReadModel lookupType) => Validate(lookupType, new LookupTypeValidator());

    private class LookupTypeValidator : AbstractValidator<LookupTypeReadModel>
    {
        public LookupTypeValidator()
        {
            RuleFor(x => x.LookupTypeName).NotEmpty().WithMessage(LookupTypeNameErrorMessage);
            RuleFor(x => x.ShortCode).NotEmpty().WithMessage(ShortCodeErrorMessage);
            RuleFor(x => x.OrderId).GreaterThanOrEqualTo(0).WithMessage(OrderIdErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<LookupTypeReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate, nameof(context.InstanceToValidate));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("LookupType", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
