using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents VIX futures end-of-day (EOD) market data for a contract and value date,
/// including OHLC prices and volume.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive members are serialized. The derived identifier
/// <see cref="EntityId"/> is excluded from serialization. Follows the same pattern as FundOrderReadModel.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record VixFuturesEodDataReadModel
{
    /// <summary>Full VIX futures contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading (value) date for this EOD snapshot.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Open price.</summary>
    [Key(2)]
    public decimal OpenPrice { get; init; }

    /// <summary>High price.</summary>
    [Key(3)]
    public decimal HighPrice { get; init; }

    /// <summary>Low price.</summary>
    [Key(4)]
    public decimal LowPrice { get; init; }

    /// <summary>Close (settlement) price.</summary>
    [Key(5)]
    public decimal ClosePrice { get; init; }

    /// <summary>Trading volume.</summary>
    [Key(6)]
    public int Volume { get; init; }

    /// <summary>
    /// Composite entity identifier (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public VixFuturesEodDataEntityId EntityId => new(ContractId ?? string.Empty, ValueDate);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public VixFuturesEodDataReadModel() { }

    /// <summary>
    /// Initializes a VIX futures EOD snapshot with OHLC and volume.
    /// </summary>
    public VixFuturesEodDataReadModel(
        string contractId,
        DateOnly valueDate,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        int volume)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        OpenPrice = openPrice;
        HighPrice = highPrice;
        LowPrice = lowPrice;
        ClosePrice = closePrice;
        Volume = volume;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

/// <summary>
/// Provides FluentValidation rules for <see cref="VixFuturesEodDataReadModel"/> instances.
/// </summary>
/// <remarks>
/// Validates VIX futures EOD data ensuring all required fields are present and valid,
/// including OHLC prices and volume consistency checks.
/// </remarks>
public class VixFuturesEodDataReadModelValidationRules : BaseValidationRules, IValidationRules<VixFuturesEodDataReadModel>
{
    public const string InstanceErrorMessage = "VixFuturesEodDataReadModel instance is null";
    public const string ContractIdErrorMessage = "VixFuturesEodDataReadModel.ContractId is required";
    public const string ValueDateErrorMessage = "VixFuturesEodDataReadModel.ValueDate is invalid";
    public const string OpenPriceErrorMessage = "VixFuturesEodDataReadModel.OpenPrice must be greater than zero";
    public const string HighPriceErrorMessage = "VixFuturesEodDataReadModel.HighPrice must be greater than zero";
    public const string LowPriceErrorMessage = "VixFuturesEodDataReadModel.LowPrice must be greater than zero";
    public const string ClosePriceErrorMessage = "VixFuturesEodDataReadModel.ClosePrice must be greater than zero";
    public const string VolumeErrorMessage = "VixFuturesEodDataReadModel.Volume must be non-negative";
    public const string HighLowErrorMessage = "VixFuturesEodDataReadModel.HighPrice must be >= LowPrice";
    public const string OhlcRangeErrorMessage = "VixFuturesEodDataReadModel OHLC prices must be within High/Low range";

    /// <summary>
    /// Executes validation rules against the specified VixFuturesEodDataReadModel instance.
    /// </summary>
    /// <param name="vixEodData">The VIX futures EOD data to validate.</param>
    /// <returns>An array of validation errors, or an empty array if validation passes.</returns>
    public ValidationError[] Execute(VixFuturesEodDataReadModel vixEodData)
        => Validate(vixEodData, new VixFuturesEodDataReadModelValidator());

    /// <summary>
    /// Internal FluentValidation validator for VixFuturesEodDataReadModel.
    /// </summary>
    class VixFuturesEodDataReadModelValidator : AbstractValidator<VixFuturesEodDataReadModel>
    {
        public VixFuturesEodDataReadModelValidator()
        {
            // ContractId validation
            RuleFor(x => x.ContractId)
                .NotEmpty()
                .WithMessage(ContractIdErrorMessage);

            // ValueDate validation
            RuleFor(x => x.ValueDate)
                .Must(valueDate => valueDate != DateOnly.MinValue && valueDate != DateOnly.MaxValue)
                .WithMessage(ValueDateErrorMessage);

            // OpenPrice validation
            RuleFor(x => x.OpenPrice)
                .GreaterThan(0)
                .WithMessage(OpenPriceErrorMessage);

            // HighPrice validation
            RuleFor(x => x.HighPrice)
                .GreaterThan(0)
                .WithMessage(HighPriceErrorMessage);

            // LowPrice validation
            RuleFor(x => x.LowPrice)
                .GreaterThan(0)
                .WithMessage(LowPriceErrorMessage);

            // ClosePrice validation
            RuleFor(x => x.ClosePrice)
                .GreaterThan(0)
                .WithMessage(ClosePriceErrorMessage);

            // Volume validation
            RuleFor(x => x.Volume)
                .GreaterThanOrEqualTo(0)
                .WithMessage(VolumeErrorMessage);

            // High >= Low validation
            RuleFor(x => x)
                .Must(x => x.HighPrice >= x.LowPrice)
                .WithMessage(HighLowErrorMessage);

            // OHLC range validation
            RuleFor(x => x)
                .Must(x => x.OpenPrice >= x.LowPrice && x.OpenPrice <= x.HighPrice &&
                           x.ClosePrice >= x.LowPrice && x.ClosePrice <= x.HighPrice)
                .WithMessage(OhlcRangeErrorMessage);
        }

        /// <summary>
        /// Overrides the base validation to ensure the instance is not null before validating properties.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <returns>The validation result.</returns>
        public override ValidationResult Validate(ValidationContext<VixFuturesEodDataReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("VixFuturesEodDataReadModel", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}