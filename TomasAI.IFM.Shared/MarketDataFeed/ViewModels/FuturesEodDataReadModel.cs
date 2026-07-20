using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents end-of-day (EOD) futures market data enriched with derived statistical and classification metrics,
/// used by analytics and signal generation components.
/// </summary>
/// <remarks>
/// This view model is MessagePack serializable for efficient transport. Only primitive / enum fields are serialized.
/// Derived identifiers and helper methods are excluded via <see cref="IgnoreMemberAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesEodDataV2ReadModel
{
    /// <summary>Full contract identifier (root + month/year code).</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value date of the market data (trading / settlement date).</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Symbol root (e.g. ES, NQ, CL, VX).</summary>
    [Key(2)]
    public string Symbol { get; init; }

    /// <summary>Open price.</summary>
    [Key(3)]
    public decimal OpenPrice { get; init; }

    /// <summary>Session high price.</summary>
    [Key(4)]
    public decimal HighPrice { get; init; }

    /// <summary>Session low price.</summary>
    [Key(5)]
    public decimal LowPrice { get; init; }

    /// <summary>Settlement / close price.</summary>
    [Key(6)]
    public decimal ClosePrice { get; init; }

    /// <summary>Session volume.</summary>
    [Key(7)]
    public int Volume { get; init; }

    /// <summary>Percent change over the analysis window (daily or rolling).</summary>
    [Key(8)]
    public double DailyPercentChange { get; init; }

    /// <summary>Rolling standard deviation (percent form).</summary>
    [Key(9)]
    public double DailyStdDev { get; init; }

    /// <summary>Rolling standard deviation expressed in price units.</summary>
    [Key(10)]
    public double DailyStdDevAmount { get; init; }

    /// <summary>Upper Bollinger / volatility band (if applicable).</summary>
    [Key(11)]
    public double UpperBand { get; init; }

    /// <summary>Rolling mean / middle band.</summary>
    [Key(12)]
    public double Mean { get; init; }

    /// <summary>Lower Bollinger / volatility band (if applicable).</summary>
    [Key(13)]
    public double LowerBand { get; init; }

    /// <summary>Classified overall market direction.</summary>
    [Key(14)]
    public MarketDirectionType MarketDirection { get; init; }

    /// <summary>Classified overall market volatility regime.</summary>
    [Key(15)]
    public MarketVolatilityType MarketVolatility { get; init; }

    /// <summary>Price direction classification.</summary>
    [Key(16)]
    public PriceDirectionType PriceDirection { get; init; }

    /// <summary>Price volatility classification.</summary>
    [Key(17)]
    public PriceVolatilityType PriceVolatility { get; init; }

    /// <summary>Directional indicator (MDI / custom composite).</summary>
    [Key(18)]
    public double MarketDirectionIndicator { get; init; }

    /// <summary>Window size used to compute rolling stats.</summary>
    [Key(19)]
    public int WindowSize { get; init; }

    /// <summary>50-day moving average (optional if pre-computed upstream).</summary>
    [Key(20)]
    public decimal FiftyDMA { get; set; }

    /// <summary>200-day moving average (optional if pre-computed upstream).</summary>
    [Key(21)]
    public decimal TwoHundredDMA { get; set; }

    /// <summary>Composite futures data identifier (not serialized).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesDataId DataId => new(ContractId ?? string.Empty, ValueDate);

    /// <summary>
    /// Parses and returns the strongly typed futures contract identifier (not serialized).
    /// </summary>
    public FuturesContractId GetContractId() => new FuturesContractIdParser(ContractId ?? string.Empty).Id;

    /// <summary>
    /// Parameterless constructor required by MessagePack and some serializers. Initializes numeric fields to zero and enums to defaults.
    /// </summary>
    public FuturesEodDataV2ReadModel() { }

    /// <summary>
    /// Full constructor enabling initialization of all serialized properties.
    /// </summary>
    public FuturesEodDataV2ReadModel(
        string contractId,
        DateOnly valueDate,
        string symbol,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        int volume,
        double dailyPercentChange = 0,
        double dailyStdDev = 0,
        double dailyStdDevAmount = 0,
        double upperBand = 0,
        double mean = 0,
        double lowerBand = 0,
        MarketDirectionType marketDirection = MarketDirectionType.NeutralUp,
        MarketVolatilityType marketVolatility = MarketVolatilityType.Normal,
        PriceDirectionType priceDirection = PriceDirectionType.Flat,
        PriceVolatilityType priceVolatility = PriceVolatilityType.Unknown,
        double marketDirectionIndicator = 0,
        int windowSize = 0,
        decimal fiftyDMA = 0,
        decimal twoHundredDMA = 0)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        Symbol = symbol;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
        DailyPercentChange = dailyPercentChange;
        DailyStdDev = dailyStdDev;
        DailyStdDevAmount = dailyStdDevAmount;
        UpperBand = upperBand;
        Mean = mean;
        LowerBand = lowerBand;
        MarketDirection = marketDirection;
        MarketVolatility = marketVolatility;
        PriceDirection = priceDirection;
        PriceVolatility = priceVolatility;
        MarketDirectionIndicator = marketDirectionIndicator;
        WindowSize = windowSize;
        FiftyDMA = fiftyDMA;
        TwoHundredDMA = twoHundredDMA;
    }

    /// <summary>
    /// Returns a JSON string representation of the view model (for diagnostics).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => !string.IsNullOrEmpty(ContractId) && ValueDate > DateOnly.MinValue;
}

/// <summary>
/// Validation rules for <see cref="FuturesEodDataV2ReadModel"/> instances.
/// </summary>
/// <remarks>
/// Provides a simple entry point (<see cref="Execute"/>) used to validate incoming futures EOD data view models
/// before they are processed. The concrete rules are implemented by the nested <see cref="FuturesEodDataV2Validator"/>
/// which uses FluentValidation to describe validation constraints.
/// </remarks>
public class FuturesEodDataValidationRules : BaseValidationRules, IValidationRules<FuturesEodDataV2ReadModel>
{
    /// <summary>
    /// Execute validation for the supplied <see cref="FuturesEodDataV2ReadModel"/>.
    /// </summary>
    /// <param name="futuresEodData">The futures EOD data view model to validate.</param>
    /// <returns>An array of <see cref="ValidationError"/> describing validation failures. Empty if valid.</returns>
    public ValidationError[] Execute(FuturesEodDataV2ReadModel futuresEodData)
        => Validate(futuresEodData, new FuturesEodDataV2Validator());

    /// <summary>
    /// FluentValidation validator describing the rules for <see cref="FuturesEodDataV2ReadModel"/>.
    /// </summary>
    /// <remarks>
    /// The validator ensures required fields are populated, numeric values are valid (not NaN or Infinity),
    /// enums have valid values, prices are non-negative, and returns a friendly error when the instance itself is null.
    /// </remarks>
    class FuturesEodDataV2Validator : AbstractValidator<FuturesEodDataV2ReadModel>
    {
        /// <summary>
        /// Initializes a new instance of <see cref="FuturesEodDataV2Validator"/> and configures validation rules.
        /// </summary>
        public FuturesEodDataV2Validator()
        {
            // Required string fields
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage("FuturesEodDataV2.ContractId is required");

            RuleFor(x => x.Symbol)
                .NotEmpty()
                .WithMessage("FuturesEodDataV2.Symbol is required");

            // Required date fields
            RuleFor(x => x.ValueDate)
                .NotEmpty()
                .WithMessage("FuturesEodDataV2.ValueDate is required");

            // Price validations - must be non-negative decimals
            RuleFor(x => x.OpenPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.OpenPrice must be non-negative");

            RuleFor(x => x.HighPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.HighPrice must be non-negative");

            RuleFor(x => x.LowPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.LowPrice must be non-negative");

            RuleFor(x => x.ClosePrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.ClosePrice must be non-negative");

            // Volume validation - must be non-negative
            RuleFor(x => x.Volume)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.Volume must be non-negative");

            // Double validations - must be valid numbers
            RuleFor(x => x.DailyPercentChange)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.DailyPercentChange is NaN or Infinity");

            RuleFor(x => x.DailyStdDev)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.DailyStdDev is NaN or Infinity");

            RuleFor(x => x.DailyStdDevAmount)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.DailyStdDevAmount is NaN or Infinity");

            RuleFor(x => x.UpperBand)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.UpperBand is NaN or Infinity");

            RuleFor(x => x.Mean)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.Mean is NaN or Infinity");

            RuleFor(x => x.LowerBand)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.LowerBand is NaN or Infinity");

            RuleFor(x => x.MarketDirectionIndicator)
                .Must(value => !double.IsNaN(value) && !double.IsInfinity(value))
                .WithMessage("FuturesEodDataV2.MarketDirectionIndicator is NaN or Infinity");

            // Enum validations
            RuleFor(x => x.MarketDirection)
                .IsInEnum()
                .WithMessage("FuturesEodDataV2.MarketDirection must be a valid enum value");

            RuleFor(x => x.MarketVolatility)
                .IsInEnum()
                .WithMessage("FuturesEodDataV2.MarketVolatility must be a valid enum value");

            RuleFor(x => x.PriceDirection)
                .IsInEnum()
                .WithMessage("FuturesEodDataV2.PriceDirection must be a valid enum value");

            RuleFor(x => x.PriceVolatility)
                .IsInEnum()
                .WithMessage("FuturesEodDataV2.PriceVolatility must be a valid enum value");

            // WindowSize should be non-negative
            RuleFor(x => x.WindowSize)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.WindowSize must be non-negative");

            // Moving averages - must be non-negative
            RuleFor(x => x.FiftyDMA)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.FiftyDMA must be non-negative");

            RuleFor(x => x.TwoHundredDMA)
                .GreaterThanOrEqualTo(0)
                .WithMessage("FuturesEodDataV2.TwoHundredDMA must be non-negative");
        }

        /// <summary>
        /// Validates the supplied <see cref="FuturesEodDataV2ReadModel"/> instance.
        /// </summary>
        /// <param name="context">The validation context containing the instance to validate.</param>
        /// <returns>A <see cref="ValidationResult"/> describing validation failures.</returns>
        /// <remarks>
        /// This override provides an explicit null-check for the instance and returns a single validation
        /// failure describing the null instance to keep error reporting consistent.
        /// </remarks>
        public override ValidationResult Validate(ValidationContext<FuturesEodDataV2ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesEodDataV2", "FuturesEodDataV2 instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}