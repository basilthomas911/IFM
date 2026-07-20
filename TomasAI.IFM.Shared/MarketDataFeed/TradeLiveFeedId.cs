using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Represents the unique identifier for a trade live feed, composed of an order identifier and a trade identifier.
/// </summary>
/// <remarks>
/// MessagePack serializable. Implements <see cref="IActorEntityId"/> and formats to a stable string key
/// using dot notation: "OrderId.TradeId".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeLiveFeedId
    : IActorEntityId
{
    /// <summary>The order identifier associated with the trade live feed.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>The trade identifier associated with the trade live feed.</summary>
    [Key(1)]
    public int TradeId { get; init; }
    
    [Key(2)]
    public DateOnly ValueDate {  get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling scenarios.
    /// </summary>
    public TradeLiveFeedId() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TradeLiveFeedId"/> record.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="valueDate">The value date.</param>
    public TradeLiveFeedId(int orderId, int tradeId, DateOnly valueDate)    
    {
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
    }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "OrderId.TradeId".
    /// </summary>
    /// <returns>The formatted identifier string.</returns>
    public string Format() => string.Create(null, stackalloc char[64], $"{OrderId}.{TradeId}.{ValueDate:yyyyMMdd}");

    [IgnoreMember]
    public bool IsValid
        => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;
}
