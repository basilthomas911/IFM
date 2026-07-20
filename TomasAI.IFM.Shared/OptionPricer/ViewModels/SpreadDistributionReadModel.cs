using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer.ViewModels;

/// <summary>
/// MessagePack-serializable snapshot of a spread distribution (put/call risk metrics) for a specific option trade
/// on a given value date.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential <see cref="KeyAttribute"/> indices,
/// derived/utility members excluded via <see cref="IgnoreMemberAttribute"/> / <see cref="JsonIgnoreAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record SpreadDistributionReadModel
{
    /// <summary>Persistence identifier (database/generated id).</summary>
    [Key(0)]
    public long Id { get; init; }

    /// <summary>Internal trade identifier this distribution belongs to.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Value (trading) date of the distribution snapshot.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Type of the option trade (strategy / leg classification).</summary>
    [Key(3)]
    public TradeType TradeType { get; init; }

    /// <summary>Status of the trade when the snapshot was generated.</summary>
    [Key(4)]
    public TradeStatus TradeStatus { get; init; }

    /// <summary>Remaining days to expiry for the underlying position.</summary>
    [Key(5)]
    public int DaysToExpiry { get; init; }

    /// <summary>Forward price used in distribution calculations.</summary>
    [Key(6)]
    public double ForwardPrice { get; init; }

    /// <summary>Probability that the forward loss threshold is breached.</summary>
    [Key(7)]
    public double LossProbability { get; init; }

    /// <summary>Configured forward loss threshold value.</summary>
    [Key(8)]
    public decimal LossThreshold { get; init; }

    /// <summary>Count of simulated paths (or observations) exceeding the loss threshold.</summary>
    [Key(9)]
    public int LossThresholdCount { get; init; }

    /// <summary>Implied volatility of the short leg.</summary>
    [Key(10)]
    public double ShortVolatility { get; init; }

    /// <summary>Implied volatility of the long leg.</summary>
    [Key(11)]
    public double LongVolatility { get; init; }

    /// <summary>Computed forward loss ratio (loss / forward reference value).</summary>
    [Key(12)]
    public double ForwardLossRatio { get; init; }

    /// <summary>UTC timestamp when this snapshot was created.</summary>
    [Key(13)]
    public DateTime CreatedOn { get; init; }

    /// <summary>
    /// Composite entity identifier (not serialized) combining trade and value date.
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public SpreadDistributionEntityId EntityId => new(TradeId, ValueDate);

    /// <summary>
    /// Basic validity flag (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => TradeId > 0 && ValueDate != default;

    /// <summary>
    /// Initializes a new spread distribution snapshot.
    /// Constructor parameter names are camelCase to match existing named-argument usages across the codebase.
    /// </summary>
    public SpreadDistributionReadModel(
        long id,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        TradeStatus tradeStatus,
        int daysToExpiry,
        double forwardPrice,
        double lossProbability,
        decimal lossThreshold,
        int lossThresholdCount,
        double shortVolatility,
        double longVolatility,
        double forwardLossRatio,
        DateTime createdOn)
    {
        Id = id;
        TradeId = tradeId;
        ValueDate = valueDate;
        TradeType = tradeType;
        TradeStatus = tradeStatus;
        DaysToExpiry = daysToExpiry;
        ForwardPrice = forwardPrice;
        LossProbability = lossProbability;
        LossThreshold = lossThreshold;
        LossThresholdCount = lossThresholdCount;
        ShortVolatility = shortVolatility;
        LongVolatility = longVolatility;
        ForwardLossRatio = forwardLossRatio;
        CreatedOn = createdOn;
    }

    /// <summary>
    /// Returns a compact JSON representation of the snapshot.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}