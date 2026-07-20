using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a trade plan forward loss limit configuration.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived members
/// are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradePlanForwardLossLimitReadModel
{
    /// <summary>Order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Value (trading) date associated with the limit.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Option trade type (strategy classification).</summary>
    [Key(3)]
    public TradeType TradeType { get; init; }

    /// <summary>Forward loss limit type (e.g., warning or reached).</summary>
    [Key(4)]
    public ForwardLossLimitType LimitType { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes enums to Unknown/defaults.
    /// </summary>
    public TradePlanForwardLossLimitReadModel()
    {
        TradeType = TradeType.Unknown;
        LimitType = ForwardLossLimitType.Unknown;
    }

    /// <summary>
    /// Creates a new trade plan forward loss limit view model.
    /// </summary>
    public TradePlanForwardLossLimitReadModel(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        ForwardLossLimitType limitType)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        TradeType = tradeType;
        LimitType = limitType;
    }

    /// <summary>
    /// Derived identifier for this forward loss limit (excluded from MessagePack).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public TradePlanForwardLossLimitEntityId EntityId => new(OrderId, TradeId, ValueDate, TradeType);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;

    /// <summary>
    /// Returns a compact JSON representation of this model.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}