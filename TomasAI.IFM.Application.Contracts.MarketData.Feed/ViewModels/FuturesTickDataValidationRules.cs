using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

/// <summary>
/// FluentValidation rules for <see cref="FuturesTickDataV2ReadModel"/>.
/// </summary>
public class FuturesTickDataValidationRules : BaseValidationRules, IValidationRules<FuturesTickDataV2ReadModel>
{
    public ValidationError[]? Execute(FuturesTickDataV2ReadModel instance)
        => Validate(instance, new FuturesTickDataV2ReadModelValidator());

    private sealed class FuturesTickDataV2ReadModelValidator : AbstractValidator<FuturesTickDataV2ReadModel>
    {
        public FuturesTickDataV2ReadModelValidator()
        {
            RuleFor(x => x.ContractId)
                .NotEmpty().WithMessage("ContractId is required and cannot be empty.");

            RuleFor(x => x.ValueDate)
                .NotEqual(DateOnly.MinValue).WithMessage("ValueDate cannot be the minimum date value.")
                .NotEqual(DateOnly.MaxValue).WithMessage("ValueDate cannot be the maximum date value.");

            RuleFor(x => x.TickId)
                .GreaterThan(0L).WithMessage("TickId must be greater than zero.");

            RuleFor(x => x.TickTime)
                .NotEqual(TimeOnly.MinValue).WithMessage("TickTime cannot be the minimum time value.")
                .NotEqual(TimeOnly.MaxValue).WithMessage("TickTime cannot be the maximum time value.");

            RuleFor(x => x.Price)
                .GreaterThanOrEqualTo(0m).WithMessage("Price cannot be negative.");

            RuleFor(x => x.Size)
                .GreaterThanOrEqualTo(0).WithMessage("Size cannot be negative.");
        }

        public override ValidationResult Validate(ValidationContext<FuturesTickDataV2ReadModel> context)
        {
            ArgumentNullException.ThrowIfNull(context);
            if (context.InstanceToValidate is null)
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesTickData", "FuturesTickData instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}