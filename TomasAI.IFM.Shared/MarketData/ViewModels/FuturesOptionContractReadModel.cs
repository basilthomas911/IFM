using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketData.ViewModels;

/// <summary>
/// Represents a futures option contract definition including identifiers, descriptive metadata,
/// financial terms (strike, multiplier), and listing attributes (exchange, currency, security type).
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive / enum fields are serialized. Derived identifiers
/// (<see cref="Id"/>, <see cref="EntityId"/>) are excluded via <see cref="IgnoreMemberAttribute"/>.
/// Utility methods for local symbol generation remain static and are not serialized.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractReadModel
{
    /// <summary>Full contract identifier string (parsed by <c>FuturesOptionContractId</c>).</summary>
    [Key(0)] public string ContractId { get; init; }

    /// <summary>Human-readable contract description.</summary>
    [Key(1)] public string Description { get; init; }

    /// <summary>Underlying futures contract root symbol (e.g. ES, NQ).</summary>
    [Key(2)] public string Symbol { get; init; }

    /// <summary>Generated local option symbol (exchange specific short code).</summary>
    [Key(3)] public string LocalSymbol { get; init; }

    /// <summary>Security type (e.g. FUT, OPT).</summary>
    [Key(4)] public string SecurityType { get; init; }

    /// <summary>Trading currency.</summary>
    [Key(5)] public string Currency { get; init; }

    /// <summary>Primary listing / execution exchange.</summary>
    [Key(6)] public string Exchange { get; init; }

    /// <summary>Contract multiplier (volume to notional conversion factor).</summary>
    [Key(7)] public string Multiplier { get; init; }

    /// <summary>Contract month (year/month used for expiry grouping).</summary>
    [Key(8)] public DateOnly ContractMonth { get; init; }

    /// <summary>Option strike price.</summary>
    [Key(9)] public double StrikePrice { get; init; }

    /// <summary>Option type code (e.g. "Call", "Put").</summary>
    [Key(10)] public string OptionType { get; init; }

    /// <summary>Strongly typed option contract identifier (not serialized).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesOptionContractId Id { get; private set; } = default!;

    /// <summary>Entity identifier grouped by contract year (not serialized).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesOptionContractEntityId EntityId { get; private set; } = default;

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesOptionContractReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    public FuturesOptionContractReadModel(
        string contractId,
        string description,
        string symbol,
        string localSymbol,
        string securityType,
        string currency,
        string exchange,
        string multiplier,
        DateOnly contractMonth,
        double strikePrice,
        string optionType)
    {
        ContractId = contractId;
        Description = description;
        Symbol = symbol;
        LocalSymbol = localSymbol;
        SecurityType = securityType;
        Currency = currency;
        Exchange = exchange;
        Multiplier = multiplier;
        ContractMonth = contractMonth;
        StrikePrice = strikePrice;
        OptionType = optionType;

        Id = new FuturesOptionContractId(ContractId ?? string.Empty);
        EntityId = new FuturesOptionContractEntityId(ContractId ?? string.Empty, contractMonth.Year);
    }

    /// <summary>
    /// Generates a combined local symbol with abbreviated option type and zero-padded strike.
    /// </summary>
    /// <param name="localSymbol">Base local symbol.</param>
    /// <param name="optionType">Option type string ("Call"/"Put").</param>
    /// <param name="strikePrice">Strike price.</param>
    /// <returns>Formatted local symbol with type and strike.</returns>
    public static string GetContractLocalSymbol(string localSymbol, string optionType, double strikePrice)
        => $"{localSymbol} {optionType.Substring(0, 1)}{strikePrice:0000}";

    /// <summary>
    /// Builds a local symbol based on underlying symbol, week number within month, contract month code and year digit.
    /// </summary>
    /// <param name="symbol">Underlying futures symbol.</param>
    /// <param name="valueDate">Date used to compute week and month codes.</param>
    /// <returns>Exchange-compatible local symbol.</returns>
    public static string GetLocalSymbol(string symbol, DateOnly valueDate)
    {
        string weekSymbol;
        var weekNumber = GetWeekNumber();
        if (weekNumber == 3 && IsContractMonth())
            weekSymbol = "S";
        else if (weekNumber > 4 || IsLastDayOfMonth())
            weekSymbol = "W";
        else
            weekSymbol = valueDate.DayOfWeek switch
            {
                DayOfWeek.Monday => $"{weekNumber}A",
                DayOfWeek.Wednesday => $"{weekNumber}C",
                _ => $"W{weekNumber}"
            };

        var assetSymbol = symbol.Substring(0, 1);
        var monthSymbol = new[] { "F", "G", "H", "J", "K", "M", "N", "Q", "U", "V", "X", "Z" }[valueDate.Month - 1];
        var yearSymbol = $"{valueDate.Year}".Substring(3, 1);
        return $"{assetSymbol}{weekSymbol}{monthSymbol}{yearSymbol}";

        int GetWeekNumber()
        {
            var startDate = new DateOnly(valueDate.Year, valueDate.Month, 1);
            var numWDays = valueDate.DayNumber - startDate.DayNumber;
            return (numWDays / 7) + 1;
        }

        bool IsContractMonth() => valueDate.Month is 3 or 6 or 9 or 12;

        bool IsLastDayOfMonth()
        {
            var nextMonth = valueDate.AddMonths(1);
            var firstDayNext = new DateOnly(nextMonth.Year, nextMonth.Month, 1);
            return firstDayNext.AddDays(-1).Equals(valueDate) || valueDate.DayOfWeek == DayOfWeek.Thursday;
        }
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember]
    public bool IsValid
        => !string.IsNullOrEmpty(ContractId) &&
           !string.IsNullOrEmpty(Symbol) &&
           !string.IsNullOrEmpty(LocalSymbol) &&
           !string.IsNullOrEmpty(SecurityType) &&
           !string.IsNullOrEmpty(Currency) &&
           !string.IsNullOrEmpty(Exchange) &&
           !string.IsNullOrEmpty(Multiplier) && ContractMonth != default && StrikePrice > 0 &&
           !string.IsNullOrEmpty(OptionType);
}

/// <summary>
/// Collection wrapper for a set of futures option contract view models.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractsReadModel
{
    [Key(0)]
    public FuturesOptionContractReadModel[] Contracts { get; init; } = [];

    public FuturesOptionContractsReadModel() { }

    public FuturesOptionContractsReadModel(FuturesOptionContractReadModel[] contracts)
        => Contracts = contracts ?? [];
}

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

    readonly IReferenceLookupService _refLookupService = refLookupService ?? throw new ArgumentNullException(nameof(refLookupService));

    public ValidationError[] Execute(FuturesOptionContractReadModel futuresOptionContract) => Validate(futuresOptionContract, new FuturesOptionContractValidator(_refLookupService));

    class FuturesOptionContractValidator : AbstractValidator<FuturesOptionContractReadModel>
    {
        public FuturesOptionContractValidator(IReferenceLookupService refLookupService)
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage(ContractIdErrorMessage);
            RuleFor(x => x.SecurityType).NotEmpty().Equal("FOP").WithMessage(SecurityTypeErrorMessage);
            RuleFor(x => x.Symbol).NotEmpty().Must(e => refLookupService.SymbolExists(e)).WithMessage(SymbolErrorMessage);
            RuleFor(x => x.LocalSymbol).NotEmpty().WithMessage(LocalSymbolErrorMessage);
            RuleFor(x => x.Currency).NotEmpty().Must(e => refLookupService.CurrencyExists(e)).WithMessage(CurrencyErrorMessage);
            RuleFor(x => x.Exchange).NotEmpty().Must(e => refLookupService.ExchangeExists(e)).WithMessage(ExchangeErrorMessage);
            RuleFor(x => x.Multiplier).NotEmpty().Must(e => refLookupService.MultiplierExists(e)).WithMessage(MultiplierErrorMessage);
            RuleFor(x => x.ContractMonth).NotEmpty().Must(e => e != DateOnly.MinValue && e != DateOnly.MaxValue).WithMessage(ContractMonthErrorMessage);
            RuleFor(x => x.StrikePrice).NotEmpty().Must(e => !double.IsNaN(e)).WithMessage(StrikePriceErrorMessage);
            RuleFor(x => x.OptionType).NotEmpty().WithMessage(OptionTypeErrorMessage);
        }

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
