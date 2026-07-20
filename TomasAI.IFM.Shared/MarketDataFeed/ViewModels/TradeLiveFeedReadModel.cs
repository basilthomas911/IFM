using MessagePack;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public record TradeLiveFeedReadModel
{
    [Key(0)]
    public int OrderId { get; init; }

    [Key(1)]
    public int TradeId { get; init; }

    [Key(2)]
    public TradeLiveFeedStateType TradeLiveFeedState { get; init; }

    public TradeLiveFeedReadModel()
    {
        TradeLiveFeedState = TradeLiveFeedStateType.Unknown;
    }

    [SerializationConstructor]
    public TradeLiveFeedReadModel(
        int orderId,
        int tradeId,
        TradeLiveFeedStateType tradeLiveFeedState)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeLiveFeedState = tradeLiveFeedState;
    }
}
