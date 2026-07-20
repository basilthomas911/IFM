using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Validation;

public class FuturesContractValidationRules(IReferenceLookupService refLookupService) : BaseValidationRules, IValidationRules<FuturesContractV2ReadModel>
{
    public const string InstanceErrorMessage = "FuturesContract instance is null";
    public const string ContractIdErrorMessage = "FuturesContractId: contractId is required";
    public const string SecurityTypeErrorMessage = "FuturesContract.SecurityType: must be FUT";
    public const string SymbolErrorMessage = "FuturesContract.Symbol is required";
    public const string LocalSymbolErrorMessage = "FuturesContract.LocalSymbol is required";
    public const string CurrencyErrorMessage = "FuturesContract.Currency is required";
    public const string ExchangeErrorMessage = "FuturesContract.Exchange is required";
    public const string MultiplierErrorMessage = "FuturesContract.Multiplier is required";
    public const string MinLastTradeDateErrorMessage = "FuturesContract.LastTradeDate must be greater than DateTime.MinValue";
    public const string MaxLastTradeDateErrorMessage = "FuturesContract.LastTradeDate must be less than DateTime.MaxValue";

    readonly IReferenceLookupService _refLookupService = refLookupService;

    public ValidationError[] Execute(FuturesContractV2ReadModel futuresContract) => Validate(futuresContract, new FuturesContractValidator(_refLookupService));

    class FuturesContractValidator : AbstractValidator<FuturesContractV2ReadModel>
    {
        public FuturesContractValidator(IReferenceLookupService refLookupService)
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.SecurityType).NotEmpty().Equal("FUT").WithMessage(SecurityTypeErrorMessage);
            RuleFor(x => x.Symbol).NotEmpty().Must(e => refLookupService.SymbolExists(e)).WithMessage(SymbolErrorMessage);
            RuleFor(x => x.LocalSymbol).NotEmpty().WithMessage(LocalSymbolErrorMessage);
            RuleFor(x => x.Currency).NotEmpty().Must(e => refLookupService.CurrencyExists(e)).WithMessage(CurrencyErrorMessage);
            RuleFor(x => x.Exchange).NotEmpty().Must(e => refLookupService.ExchangeExists(e)).WithMessage(ExchangeErrorMessage);
            RuleFor(x => x.Multiplier).NotEmpty().Must(e => refLookupService.MultiplierExists(e)).WithMessage(MultiplierErrorMessage);
            RuleFor(x => x.LastTradeDate).NotEqual(DateOnly.MinValue).WithMessage(MinLastTradeDateErrorMessage);
            RuleFor(x => x.LastTradeDate).NotEqual(DateOnly.MaxValue).WithMessage(MaxLastTradeDateErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<FuturesContractV2ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesContract", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
        
    }
}
