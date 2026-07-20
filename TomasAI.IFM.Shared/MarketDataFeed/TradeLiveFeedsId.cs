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
public record TradeLiveFeedsId
    : IActorEntityId
{
    /// <summary>The order identifier associated with the trade live feed.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling scenarios.
    /// </summary>
    public TradeLiveFeedsId() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TradeLiveFeedId"/> record.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    public TradeLiveFeedsId(int orderId)    
    {
        OrderId = orderId;
    }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "OrderId.TradeId".
    /// </summary>
    /// <returns>The formatted identifier string.</returns>
    public string Format() => string.Create(null, stackalloc char[64], $"{OrderId}");

    [IgnoreMember]
    public bool IsValid
        => OrderId > 0;
}
