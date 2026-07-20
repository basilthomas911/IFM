using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketData.Validation;

public class FuturesContractEntityIdValidationRules : BaseValidationRules, IValidationRules<FuturesContractId>
{
    public const string InstanceErrorMessage = "FuturesContractEntityId instance is null";
    public const string ContractIdErrorMessage = "FuturesContractEntityId: contractId is required";
    public const string SymbolErrorMessage = "FuturesContractEntityId: symbol is required";
    public const string MaturityDateMinErrorMessage = "FuturesContractEntityId: MaturityDate must be greater than DateOnly.MinValue";
    public const string MaturityDateMaxErrorMessage = "FuturesContractEntityId: MaturityDate must be less than DateOnly.MaxValue";

    public ValidationError[] Execute(FuturesContractId contractId) => Validate(contractId, new FuturesContractEntityIdValidator());

    class FuturesContractEntityIdValidator : AbstractValidator<FuturesContractId>
    {
        public FuturesContractEntityIdValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.Symbol).NotEmpty().WithMessage(SymbolErrorMessage);
            RuleFor(x => x.MaturityDate).NotEqual(DateOnly.MinValue).WithMessage(MaturityDateMinErrorMessage);
            RuleFor(x => x.MaturityDate).NotEqual(DateOnly.MaxValue).WithMessage(MaturityDateMaxErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<FuturesContractId> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesContractEntityId", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
