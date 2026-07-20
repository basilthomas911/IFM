using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents a single futures option tick snapshot including prices, sizes, Greeks, and timing metadata.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive and enum members are serialized. The derived identifier
/// <see cref="EntityId"/> is excluded from serialization. Follows the same pattern as FundOrderReadModel.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionTickDataV2ReadModel
{
    /// <summary>Full futures option contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Value (trading) date for which this tick applies.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Monotonic tick identifier within the contract.</summary>
    [Key(2)]
    public long TickId { get; init; }

    /// <summary>Intraday time component when the tick occurred.</summary>
    [Key(3)]
    public TimeOnly TickTime { get; init; }

    /// <summary>Option last traded price at the tick time.</summary>
    [Key(4)]
    public double OptionPrice { get; init; }

    /// <summary>Best bid price.</summary>
    [Key(5)]
    public double BidPrice { get; init; }

    /// <summary>Best ask price.</summary>
    [Key(6)]
    public double AskPrice { get; init; }

    /// <summary>Best bid size (quantity).</summary>
    [Key(7)]
    public int BidSize { get; init; }

    /// <summary>Best ask size (quantity).</summary>
    [Key(8)]
    public int AskSize { get; init; }

    /// <summary>Model-implied volatility at the tick time.</summary>
    [Key(9)]
    public double ImpliedVolatility { get; init; }

    /// <summary>Underlying futures price at the tick time.</summary>
    [Key(10)]
    public double UnderlyingPrice { get; init; }

    /// <summary>Option Delta.</summary>
    [Key(11)]
    public double Delta { get; init; }

    /// <summary>Option Gamma.</summary>
    [Key(12)]
    public double Gamma { get; init; }

    /// <summary>Option Vega.</summary>
    [Key(13)]
    public double Vega { get; init; }

    /// <summary>Option Theta.</summary>
    [Key(14)]
    public double Theta { get; init; }

    /// <summary>Option Rho.</summary>
    [Key(15)]
    public double Rho { get; init; }

    /// <summary>
    /// Composite entity identifier (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesOptionTickEntityId EntityId => FuturesOptionTickEntityId.Create(ContractId ?? string.Empty, ValueDate, TickId);

    /// <summary>
    /// Indicates whether the tick is empty or uninitialized.
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public bool IsEmpty => string.IsNullOrEmpty(ContractId) || ValueDate == DateOnly.MinValue || TickId == 0;

    /// <summary>
    /// Default empty tick instance.
    /// </summary>
    public static FuturesOptionTickDataV2ReadModel Default => new(
        string.Empty,
        DateOnly.MinValue,
        0,
        TimeOnly.MinValue,
        0.0,
        0.0,
        0.0,
        0,
        0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesOptionTickDataV2ReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    public FuturesOptionTickDataV2ReadModel(
        string contractId,
        DateOnly valueDate,
        long tickId,
        TimeOnly tickTime,
        double optionPrice,
        double bidPrice,
        double askPrice,
        int bidSize,
        int askSize,
        double impliedVolatility,
        double underlyingPrice,
        double delta,
        double gamma,
        double vega,
        double theta,
        double rho)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TickId = tickId;
        TickTime = tickTime;
        OptionPrice = optionPrice;
        BidPrice = bidPrice;
        AskPrice = askPrice;
        BidSize = bidSize;
        AskSize = askSize;
        ImpliedVolatility = impliedVolatility;
        UnderlyingPrice = underlyingPrice;
        Delta = delta;
        Gamma = gamma;
        Vega = vega;
        Theta = theta;
        Rho = rho;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember]
    public bool IsValid
        => ContractId != null && ValueDate > DateOnly.MinValue && TickId > 0;
}

/// <summary>
/// Provides FluentValidation rules for <see cref="FuturesOptionTickDataV2ReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates futures option tick data ensuring all required fields are present and valid.
/// Does NOT check for null reference value (per requirements).
/// </remarks>
public class FuturesOptionTickDataV2ReadModelValidationRules : BaseValidationRules, IValidationRules<FuturesOptionTickDataV2ReadModel>
{
    public const string InstanceErrorMessage = "FuturesOptionTickDataV2ReadModel instance is null";
    public const string ContractIdErrorMessage = "FuturesOptionTickDataV2ReadModel.ContractId is required";
    public const string ValueDateErrorMessage = "FuturesOptionTickDataV2ReadModel.ValueDate is invalid";
    public const string TickIdErrorMessage = "FuturesOptionTickDataV2ReadModel.TickId must be greater than zero";
    public const string TickTimeErrorMessage = "FuturesOptionTickDataV2ReadModel.TickTime is invalid";
    public const string OptionPriceErrorMessage = "FuturesOptionTickDataV2ReadModel.OptionPrice must be non-negative";
    public const string BidPriceErrorMessage = "FuturesOptionTickDataV2ReadModel.BidPrice must be non-negative";
    public const string AskPriceErrorMessage = "FuturesOptionTickDataV2ReadModel.AskPrice must be non-negative";
    public const string BidSizeErrorMessage = "FuturesOptionTickDataV2ReadModel.BidSize must be non-negative";
    public const string AskSizeErrorMessage = "FuturesOptionTickDataV2ReadModel.AskSize must be non-negative";
    public const string ImpliedVolatilityErrorMessage = "FuturesOptionTickDataV2ReadModel.ImpliedVolatility is invalid";
    public const string UnderlyingPriceErrorMessage = "FuturesOptionTickDataV2ReadModel.UnderlyingPrice must be non-negative";
    public const string DeltaErrorMessage = "FuturesOptionTickDataV2ReadModel.Delta is invalid";
    public const string GammaErrorMessage = "FuturesOptionTickDataV2ReadModel.Gamma is invalid";
    public const string VegaErrorMessage = "FuturesOptionTickDataV2ReadModel.Vega is invalid";
    public const string ThetaErrorMessage = "FuturesOptionTickDataV2ReadModel.Theta is invalid";
    public const string RhoErrorMessage = "FuturesOptionTickDataV2ReadModel.Rho is invalid";

    /// <summary>
    /// Executes validation rules against the specified FuturesOptionTickDataV2ReadModel instance.
    /// </summary>
    /// <param name="futuresOptionTickData">The futures option tick data to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(FuturesOptionTickDataV2ReadModel futuresOptionTickData)
        => Validate(futuresOptionTickData, new FuturesOptionTickDataV2ReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for FuturesOptionTickDataV2ReadModel.
    /// </summary>
    class FuturesOptionTickDataV2ReadModelValidator : AbstractValidator<FuturesOptionTickDataV2ReadModel>
    {
        public FuturesOptionTickDataV2ReadModelValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage(ContractIdErrorMessage);

            // ValueDate validation
            RuleFor(x => x.ValueDate)
                .Must(vd => vd != DateOnly.MinValue && vd != DateOnly.MaxValue)
                .WithMessage(ValueDateErrorMessage);

            // TickId validation
            RuleFor(x => x.TickId)
                .GreaterThan(0)
                .WithMessage(TickIdErrorMessage);

            // TickTime validation
            RuleFor(x => x.TickTime)
                .Must(tt => tt != TimeOnly.MinValue && tt != TimeOnly.MaxValue)
                .WithMessage(TickTimeErrorMessage);

            // OptionPrice validation
            RuleFor(x => x.OptionPrice)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage(OptionPriceErrorMessage);

            // BidPrice validation
            RuleFor(x => x.BidPrice)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage(BidPriceErrorMessage);

            // AskPrice validation
            RuleFor(x => x.AskPrice)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage(AskPriceErrorMessage);

            // BidSize validation
            RuleFor(x => x.BidSize)
                .GreaterThanOrEqualTo(0)
                .WithMessage(BidSizeErrorMessage);

            // AskSize validation
            RuleFor(x => x.AskSize)
                .GreaterThanOrEqualTo(0)
                .WithMessage(AskSizeErrorMessage);

            // ImpliedVolatility validation
            RuleFor(x => x.ImpliedVolatility)
                .Must(iv => !double.IsNaN(iv) && !double.IsInfinity(iv))
                .WithMessage(ImpliedVolatilityErrorMessage);

            // UnderlyingPrice validation
            RuleFor(x => x.UnderlyingPrice)
                .Must(p => !double.IsNaN(p) && !double.IsInfinity(p) && p >= 0)
                .WithMessage(UnderlyingPriceErrorMessage);

            // Delta validation
            RuleFor(x => x.Delta)
                .Must(d => !double.IsNaN(d) && !double.IsInfinity(d))
                .WithMessage(DeltaErrorMessage);

            // Gamma validation
            RuleFor(x => x.Gamma)
                .Must(g => !double.IsNaN(g) && !double.IsInfinity(g))
                .WithMessage(GammaErrorMessage);

            // Vega validation
            RuleFor(x => x.Vega)
                .Must(v => !double.IsNaN(v) && !double.IsInfinity(v))
                .WithMessage(VegaErrorMessage);

            // Theta validation
            RuleFor(x => x.Theta)
                .Must(t => !double.IsNaN(t) && !double.IsInfinity(t))
                .WithMessage(ThetaErrorMessage);

            // Rho validation
            RuleFor(x => x.Rho)
                .Must(r => !double.IsNaN(r) && !double.IsInfinity(r))
                .WithMessage(RhoErrorMessage);
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<FuturesOptionTickDataV2ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesOptionTickDataV2ReadModel", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}