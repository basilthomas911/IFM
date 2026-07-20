using MessagePack;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public record TradePriceReadModel
{
    [Key(0)]
    public int TradeId { get; init; }
    [Key(1)]
    public DateOnly ValueDate { get; init; }
    [Key(2)]
    public decimal NetPrice { get; init; }
    [Key(3)]
    public decimal NetForwardPrice { get; init; }

    public TradePriceReadModel() { }

    [SerializationConstructor]
    public TradePriceReadModel(int tradeId, DateOnly valueDate, decimal netPrice, decimal netForwardPrice)
    {
        TradeId = tradeId;
        ValueDate = valueDate;
        NetPrice = netPrice;
        NetForwardPrice = netForwardPrice;
    }

    [IgnoreMember]
    public bool IsValid => TradeId > 0 && ValueDate > DateOnly.MinValue;
}