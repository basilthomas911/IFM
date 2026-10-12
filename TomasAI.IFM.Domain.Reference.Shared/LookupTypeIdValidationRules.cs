using MessagePack;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Shared;

public sealed class LookupTypeIdValidationRules : BaseValidationRules, IValidationRules<LookupTypeId>
{
    static readonly LookupTypeIdValidator Validator = new();

    public ValidationError[] Execute(LookupTypeId lookupTypeId) => Validate(lookupTypeId, Validator);

    sealed class LookupTypeIdValidator : AbstractValidator<LookupTypeId>
    {
        public LookupTypeIdValidator()
        {
            RuleFor(x => x.LookupTypeName).NotEmpty().WithMessage("LookupTypeId.LookupTypeName is empty");
            RuleFor(x => x.OrderId).GreaterThanOrEqualTo(0).WithMessage("LookupTypeId.OrderId must be non-negative");
        }

        public override ValidationResult Validate(ValidationContext<LookupTypeId> context)
            => context.InstanceToValidate is null
                ? new ValidationResult([new ValidationFailure("LookupTypeId", "LookupTypeId instance is null")])
                : base.Validate(context);
    }
}
