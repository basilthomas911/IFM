using MessagePack;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing PnL for a fund trade on a given value date.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys and
/// a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundPnlReadModel
{
    /// <summary>The fund identifier.</summary>
    [Key(0)]
    public int FundId { get; init; }

    /// <summary>The value (as-of) date for the PnL.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>The order identifier within the fund.</summary>
    [Key(2)]
    public int OrderId { get; init; }

    /// <summary>The trade identifier within the order.</summary>
    [Key(3)]
    public int TradeId { get; init; }

    /// <summary>The trade strategy/type associated with the PnL.</summary>
    [Key(4)]
    public TradeType TradeType { get; init; }

    /// <summary>The profit and loss amount.</summary>
    [Key(5)]
    public decimal Pnl { get; init; }

    /// <summary>
    /// Parameterless constructor for MessagePack and other serializers.
    /// </summary>
    public FundPnlReadModel() { }

    /// <summary>
    /// Full constructor to create a fund PnL snapshot.
    /// </summary>
    public FundPnlReadModel(
        int fundId,
        DateOnly valueDate,
        int orderId,
        int tradeId,
        TradeType tradeType,
        decimal pnl)
    {
        FundId = fundId;
        ValueDate = valueDate;
        OrderId = orderId;
        TradeId = tradeId;
        TradeType = tradeType;
        Pnl = pnl;
    }

    [IgnoreMember]
    public bool IsValid => FundId > 0 && OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;
}