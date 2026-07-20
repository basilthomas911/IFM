using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents a single tick of futures market data, including contract, timestamp, price, and size.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive and enum members are serialized. The derived identifier
/// <see cref="DataId"/> is excluded from serialization. Follows the serialization pattern used by
/// <c>FundOrderReadModel</c>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTickDataV2ReadModel
{
    /// <summary>Full futures contract identifier (root + contract month/year code).</summary>
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

    /// <summary>Last traded price at the tick time.</summary>
    [Key(4)]
    public decimal Price { get; init; }

    /// <summary>Trade size (volume) at the tick time.</summary>
    [Key(5)]
    public int Size { get; init; }

    /// <summary>
    /// Composite identifier for the futures data (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesDataId DataId => new(ContractId ?? string.Empty, ValueDate);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesTickDataV2ReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized tick properties.
    /// </summary>
    /// <param name="contractId">Full futures contract identifier.</param>
    /// <param name="valueDate">Trading date for the tick.</param>
    /// <param name="tickId">Monotonic tick identifier.</param>
    /// <param name="tickTime">Intraday time of the tick.</param>
    /// <param name="price">Last traded price.</param>
    /// <param name="size">Trade size at the tick.</param>
    public FuturesTickDataV2ReadModel(
        string contractId,
        DateOnly valueDate,
        long tickId,
        TimeOnly tickTime,
        decimal price,
        int size)
    {
        ContractId = contractId;
        ValueDate = valueDate;
        TickId = tickId;
        TickTime = tickTime;
        Price = price;
        Size = size;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [JsonIgnore]
    [IgnoreMember]
    public static FuturesTickDataV2ReadModel Default
        => new(
            contractId: string.Empty,
            valueDate: DateOnly.MinValue,
            tickId: 0L,
            tickTime: TimeOnly.MinValue,
            price: 0m,
            size: 0);

    [JsonIgnore]
    [IgnoreMember]  
    public bool IsValid
        => ContractId != string.Empty && ValueDate > DateOnly.MinValue && TickId > 0L && TickTime > TimeOnly.MinValue;
}

/// <summary>
/// FluentValidation rules for <see cref="FuturesTickDataV2ReadModel"/>.
/// </summary>
public class FuturesTickDataValidationRules : BaseValidationRules, IValidationRules<FuturesTickDataV2ReadModel>
{
    public ValidationError[]? Execute(FuturesTickDataV2ReadModel instance)
        => Validate(instance, new FuturesTickDataV2ReadModelValidator());

    private class FuturesTickDataV2ReadModelValidator : AbstractValidator<FuturesTickDataV2ReadModel>
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
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesTickData", "FuturesTickData instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}