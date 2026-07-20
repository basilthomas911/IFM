using MessagePack;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model indicating whether live feed is enabled for a trade.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeLiveFeedReadModel
{
    /// <summary>Order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>True if live feed is enabled for the trade.</summary>
    [Key(2)]
    public bool LiveFeed { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradeLiveFeedReadModel() { }

    /// <summary>
    /// Initializes a new instance of the live feed view model.
    /// </summary>
    /// <param name="orderId">Order identifier.</param>
    /// <param name="tradeId">Trade identifier within the order.</param>
    /// <param name="liveFeed">True if live feed is enabled.</param>
    public TradeLiveFeedReadModel(int orderId, int tradeId, bool liveFeed)
    {
        OrderId = orderId;
        TradeId = tradeId;
        LiveFeed = liveFeed;
    }

    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0;
}