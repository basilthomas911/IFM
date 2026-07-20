using MessagePack;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing per-trade type limit thresholds
/// (max loss limit, min/max profit limits) for a trade.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeTypeLimitReadModel
{
    /// <summary>Trade identifier.</summary>
    [Key(0)]
    public int TradeId { get; init; }

    /// <summary>Trade strategy/type.</summary>
    [Key(1)]
    public TradeType TradeType { get; init; }

    /// <summary>Maximum allowed loss for this trade type.</summary>
    [Key(2)]
    public decimal MaxLossLimit { get; init; }

    /// <summary>Minimum profit threshold for this trade type.</summary>
    [Key(3)]
    public decimal MinProfitLimit { get; init; }

    /// <summary>Maximum profit threshold for this trade type.</summary>
    [Key(4)]
    public decimal MaxProfitLimit { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradeTypeLimitReadModel() { }

    /// <summary>
    /// Full constructor to initialize a trade type limit configuration.
    /// </summary>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="tradeType">Trade strategy/type.</param>
    /// <param name="maxLossLimit">Maximum allowed loss.</param>
    /// <param name="minProfitLimit">Minimum profit threshold.</param>
    /// <param name="maxProfitLimit">Maximum profit threshold.</param>
    public TradeTypeLimitReadModel(
        int tradeId,
        TradeType tradeType,
        decimal maxLossLimit,
        decimal minProfitLimit,
        decimal maxProfitLimit)
    {
        TradeId = tradeId;
        TradeType = tradeType;
        MaxLossLimit = maxLossLimit;
        MinProfitLimit = minProfitLimit;
        MaxProfitLimit = maxProfitLimit;
    }

    [IgnoreMember]
    public bool IsValid => TradeId > 0;

    /// <summary>
    /// Creates a shallow copy of this view model.
    /// </summary>
    public TradeTypeLimitReadModel Copy() => this with { };
}