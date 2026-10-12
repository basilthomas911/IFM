using TomasAI.IFM.Domain.MarketData.Shared;
using MessagePack;
using Newtonsoft.Json;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

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
    [Key(16)] public long? Volume { get; init; }
    [Key(17)] public long? OpenInterest { get; init; }
    [Key(18)] public DateOnly? VolumeValueDate { get; init; }
    [Key(19)] public DateOnly? OpenInterestValueDate { get; init; }

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

    /// <summary>Observed option price; the enclosing market observation identifies trade versus quote midpoint.</summary>
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

    /// <summary>Gets whether these sensitivities were calculated successfully; null preserves legacy wire behavior.</summary>
    [Key(20)] public bool? GreeksAvailable { get; init; }

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
