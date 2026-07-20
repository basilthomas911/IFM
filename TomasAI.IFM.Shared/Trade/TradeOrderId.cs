using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// Unique identifier for a trade order composed of an order identifier and a trade identifier.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a
/// dot-separated key format: OrderId.TradeId. Includes JSON-based string representation for diagnostics.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeOrderId : IActorEntityId
{
    /// <summary>The unique order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>The unique trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public TradeOrderId() { }

    /// <summary>
    /// Initializes a new <see cref="TradeOrderId"/> with the specified components.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    public TradeOrderId(int orderId, int tradeId)
    {
        OrderId = orderId;
        TradeId = tradeId;
    }

    /// <summary>
    /// Formats the identifier into a stable string key: OrderId.TradeId
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{OrderId}.{TradeId}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    // Preserved for compatibility with existing code referencing OptionTradeEntityId.Empty
    public static OptionTradeEntityId Empty => new(0, 0);
}
