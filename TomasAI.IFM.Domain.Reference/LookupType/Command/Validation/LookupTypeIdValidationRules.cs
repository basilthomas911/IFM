using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Reference;

namespace TomasAI.IFM.Domain.Reference.LookupType.Command.Validation;

/// <summary>
/// Provides validation rules for the LookupTypeId entity, ensuring that its properties meet required constraints.
/// </summary>
/// <remarks>This class defines validation logic for LookupTypeId instances, including checks for non-empty names
/// and non-negative order IDs. It can be used to validate LookupTypeId objects before processing or persisting
/// them.</remarks>
public class LookupTypeIdValidationRules : BaseValidationRules, IValidationRules<LookupTypeId>
{
    public const string InstanceErrorMessage = "LookupTypeId instance is null";
    public const string LookupTypeNameErrorMessage = "LookupTypeName is empty";
    public const string OrderIdErrorMessage = "OrderId must be non-negative";

    public ValidationError[] Execute(LookupTypeId lookupTypeId) => Validate(lookupTypeId, new LookupTypeIdValidator());

    private class LookupTypeIdValidator : AbstractValidator<LookupTypeId>
    {
        public LookupTypeIdValidator()
        {
            RuleFor(x => x.LookupTypeName)
                .NotEmpty()
                .WithMessage(LookupTypeNameErrorMessage);

            RuleFor(x => x.OrderId)
                .GreaterThanOrEqualTo(0)
                .WithMessage(OrderIdErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<LookupTypeId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate, nameof(context.InstanceToValidate));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("LookupTypeId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
