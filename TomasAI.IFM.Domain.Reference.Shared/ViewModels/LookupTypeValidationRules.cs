using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

public sealed class LookupTypeValidationRules : BaseValidationRules, IValidationRules<LookupTypeReadModel>
{
    static readonly LookupTypeValidator Validator = new();

    public ValidationError[] Execute(LookupTypeReadModel lookupType) => Validate(lookupType, Validator);

    sealed class LookupTypeValidator : AbstractValidator<LookupTypeReadModel>
    {
        public LookupTypeValidator()
        {
            RuleFor(x => x.LookupTypeName).NotEmpty().WithMessage("LookupType.LookupTypeName is empty");
            RuleFor(x => x.ShortCode).NotEmpty().WithMessage("LookupType.ShortCode is empty");
            RuleFor(x => x.OrderId).GreaterThanOrEqualTo(0).WithMessage("LookupType.OrderId must be non-negative");
            RuleFor(x => x.Description).NotNull().WithMessage("LookupType.Description is null");
            RuleFor(x => x.CreatedOn)
                .Must(value => value > DateTime.MinValue && value < DateTime.MaxValue)
                .WithMessage("LookupType.CreatedOn is invalid");
            RuleFor(x => x.CreatedBy).NotEmpty().WithMessage("LookupType.CreatedBy is empty");
        }

        public override ValidationResult Validate(ValidationContext<LookupTypeReadModel> context)
            => context.InstanceToValidate is null
                ? new ValidationResult([new ValidationFailure("LookupType", "LookupType instance is null")])
                : base.Validate(context);
    }
}
