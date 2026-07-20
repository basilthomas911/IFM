using MessagePack;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a trade history snapshot,
/// including status, dates, financials, and audit information.
/// </summary>
/// <remarks>
/// Pattern mirrors TradePlanForwardLossLimitReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeHistoryReadModel
{
    /// <summary>Order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Trade strategy/type.</summary>
    [Key(2)]
    public TradeType TradeType { get; init; }

    /// <summary>As-of (value) date for the history snapshot.</summary>
    [Key(3)]
    public DateOnly ValueDate { get; init; }

    /// <summary>Remaining days to expiry at the time of the snapshot.</summary>
    [Key(4)]
    public int DaysToExpiry { get; init; }

    /// <summary>Trade status at the time of the snapshot.</summary>
    [Key(5)]
    public TradeStatus TradeStatus { get; init; }

    /// <summary>Commission applied (if any).</summary>
    [Key(6)]
    public decimal? Commission { get; init; }

    /// <summary>Net spread value at the time of the snapshot.</summary>
    [Key(7)]
    public decimal NetSpread { get; init; }

    /// <summary>Trade profit and loss at the time of the snapshot.</summary>
    [Key(8)]
    public decimal TradePnl { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradeHistoryReadModel() { }

    /// <summary>
    /// Initializes a new trade history snapshot.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier within the order.</param>
    /// <param name="tradeType">Trade strategy/type.</param>
    /// <param name="valueDate">As-of (value) date.</param>
    /// <param name="daysToExpiry">Remaining days to expiry.</param>
    /// <param name="tradeStatus">Trade status at the snapshot time.</param>
    /// <param name="commission">Commission applied (if any).</param>
    /// <param name="netSpread">Net spread value.</param>
    /// <param name="tradePnl">Profit and loss value.</param>
    public TradeHistoryReadModel(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        int daysToExpiry,
        TradeStatus tradeStatus,
        decimal? commission,
        decimal netSpread,
        decimal tradePnl)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        TradeStatus = tradeStatus;
        Commission = commission;
        NetSpread = netSpread;
        TradePnl = tradePnl;
    }

    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;
}