using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable model representing spread bar data for an option trade at a specific bar timestamp.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived members
/// are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record OptionTradeSpreadBarsDataModel
{
    /// <summary>Order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Value (trading) date that the data belongs to.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Option trade strategy/type.</summary>
    [Key(3)]
    public TradeType TradeType { get; init; }

    /// <summary>Bar timestamp for this spread data point.</summary>
    [Key(4)]
    public DateTime BarDate { get; init; }

    /// <summary>Configured loss limit at the time of the bar.</summary>
    [Key(5)]
    public decimal LossLimit { get; init; }

    /// <summary>Configured win limit at the time of the bar.</summary>
    [Key(6)]
    public decimal WinLimit { get; init; }

    /// <summary>Forward spread value at the bar timestamp.</summary>
    [Key(7)]
    public decimal ForwardSpread { get; init; }

    /// <summary>Net (resulting) spread value at the bar timestamp.</summary>
    [Key(8)]
    public decimal NetSpread { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public OptionTradeSpreadBarsDataModel()
    {
        TradeType = TradeType.Unknown;
    }

    /// <summary>
    /// Creates a new option trade spread bars data model.
    /// </summary>
    public OptionTradeSpreadBarsDataModel(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        DateTime barDate,
        decimal lossLimit,
        decimal winLimit,
        decimal forwardSpread,
        decimal netSpread)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        TradeType = tradeType;
        BarDate = barDate;
        LossLimit = lossLimit;
        WinLimit = winLimit;
        ForwardSpread = forwardSpread;
        NetSpread = netSpread;
    }

    /// <summary>
    /// Derived identifier for this spread bar data record (excluded from MessagePack).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public OptionTradeSpreadBarDataId Id => new(OrderId, TradeId, ValueDate, TradeType, BarDate);

    /// <summary>
    /// Returns a compact JSON representation of this model.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}