using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable model representing spread data for an option trade at a specific sequence (tick).
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived members
/// are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record OptionTradeSpreadsDataModel
{
    /// <summary>Parent order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Value (trading) date associated with this spread data.</summary>
    [Key(2)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Option trade strategy/type.</summary>
    [Key(3)]
    public TradeType TradeType { get; init; }

    /// <summary>Monotonic sequence identifier for this spread data snapshot.</summary>
    [Key(4)]
    public long SequenceId { get; init; }

    /// <summary>Configured loss limit at this snapshot.</summary>
    [Key(5)]
    public decimal LossLimit { get; init; }

    /// <summary>Configured win limit at this snapshot.</summary>
    [Key(6)]
    public decimal WinLimit { get; init; }

    /// <summary>Forward spread value at this snapshot.</summary>
    [Key(7)]
    public decimal ForwardSpread { get; init; }

    /// <summary>Net spread value at this snapshot.</summary>
    [Key(8)]
    public decimal NetSpread { get; init; }

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(9)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or system that created this record.</summary>
    [Key(10)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor for serializers; initializes to sensible defaults.
    /// </summary>
    public OptionTradeSpreadsDataModel()
    {
        TradeType = TradeType.Unknown;
        CreatedOn = DateTime.UtcNow;
        CreatedBy = string.Empty;
    }

    /// <summary>
    /// Creates a new option trade spreads data model.
    /// </summary>
    public OptionTradeSpreadsDataModel(
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        long sequenceId,
        decimal lossLimit,
        decimal winLimit,
        decimal forwardSpread,
        decimal netSpread,
        DateTime createdOn,
        string createdBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        TradeType = tradeType;
        SequenceId = sequenceId;
        LossLimit = lossLimit;
        WinLimit = winLimit;
        ForwardSpread = forwardSpread;
        NetSpread = netSpread;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>
    /// Derived identifier for this spread data record (excluded from MessagePack).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public OptionTradeSpreadDataId Id => new(OrderId, TradeId, ValueDate, TradeType, SequenceId);

    /// <summary>
    /// Returns a compact JSON representation of this model.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}