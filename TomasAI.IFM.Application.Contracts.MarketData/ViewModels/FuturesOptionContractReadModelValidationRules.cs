using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesOptionContractReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates futures option contract data ensuring all required fields are present and valid,
/// including contract identifiers, financial terms, and listing attributes.
/// Does NOT check for null reference value (per requirements).
/// </remarks>
public class FuturesOptionContractReadModelValidationRules(IReferenceLookupService refLookupService) : BaseValidationRules, IValidationRules<FuturesOptionContractReadModel>
{
    public const string InstanceErrorMessage = "FuturesOptionContract instance is null";
    public const string ContractIdErrorMessage = "FuturesOptionContract.ContractId is required";
    public const string SecurityTypeErrorMessage = "FuturesOptionContract.SecurityType must be FOP";
    public const string SymbolErrorMessage = "FuturesOptionContract.Symbol is required";
    public const string LocalSymbolErrorMessage = "FuturesOptionContract.LocalSymbol is required";
    public const string CurrencyErrorMessage = "FuturesOptionContract.Currency is required";
    public const string ExchangeErrorMessage = "FuturesOptionContract.Exchange is required";
    public const string MultiplierErrorMessage = "FuturesOptionContract.Multiplier is required";
    public const string ContractMonthErrorMessage = "FuturesOptionContract.ContractMonth is required";
    public const string StrikePriceErrorMessage = "FuturesOptionContract.StrikePrice must be valid number";
    public const string OptionTypeErrorMessage = "FuturesOptionContract.OptionType is required";

    readonly FuturesOptionContractValidator _validator = new(
        refLookupService ?? throw new ArgumentNullException(nameof(refLookupService)));

    public ValidationError[] Execute(FuturesOptionContractReadModel futuresOptionContract)
        => Validate(futuresOptionContract, _validator);

    sealed class FuturesOptionContractValidator : AbstractValidator<FuturesOptionContractReadModel>
    {
        public FuturesOptionContractValidator(IReferenceLookupService refLookupService)
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.SecurityType).NotEmpty().Equal("FOP").WithMessage(SecurityTypeErrorMessage);
            RuleFor(x => x.Symbol).NotEmpty().Must((c, e) => c.SchemaVersion == 1 || refLookupService.SymbolExists(e)).WithMessage(SymbolErrorMessage);
            RuleFor(x => x.LocalSymbol).NotEmpty().WithMessage(LocalSymbolErrorMessage);
            RuleFor(x => x.Currency).NotEmpty().Must((c, e) => c.SchemaVersion == 1 || refLookupService.CurrencyExists(e)).WithMessage(CurrencyErrorMessage);
            RuleFor(x => x.Exchange).NotEmpty().Must((c, e) => c.SchemaVersion == 1 || refLookupService.ExchangeExists(e)).WithMessage(ExchangeErrorMessage);
            RuleFor(x => x.Multiplier).NotEmpty().Must((c, e) => c.SchemaVersion == 1
                ? decimal.TryParse(e, System.Globalization.NumberStyles.AllowDecimalPoint,
                    System.Globalization.CultureInfo.InvariantCulture, out var multiplier) && multiplier > 0 && multiplier == c.MultiplierValue
                : refLookupService.MultiplierExists(e)).WithMessage(MultiplierErrorMessage);
            RuleFor(x => x.ContractMonth).NotEmpty().Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage(ContractMonthErrorMessage);
            RuleFor(x => x.StrikePrice).NotEmpty().Must(e => !double.IsNaN(e)).WithMessage(StrikePriceErrorMessage);
            RuleFor(x => x.OptionType).NotEmpty().WithMessage(OptionTypeErrorMessage);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "The existing model/command Validate method validates object properties, not method parameters. Preserve ArgumentException and property-specific diagnostic labels consumed by validation/error adapters.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Validation collects failure details instead of throwing; compatibility lookup helpers preserve their documented fallback when input data is unavailable.")]
        public override ValidationResult Validate(ValidationContext<FuturesOptionContractReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesOptionContract", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
