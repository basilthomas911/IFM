using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// MessagePack-serializable identifier for a trade position, composed of
/// order and trade identifiers, value date, trade type, status, and days to expiry.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "OrderId.TradeId.ValueDate.TradeType.TradeStatus.DaysToExpiry".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record struct TradePositionEntityId(
    /// <summary>Order identifier.</summary>
    [property: Key(0)] int OrderId,
    /// <summary>Trade identifier within the order.</summary>
    [property: Key(1)] int TradeId,
    /// <summary>As-of (value) date.</summary>
    [property: Key(2)] DateOnly ValueDate,
    /// <summary>Trade strategy/type.</summary>
    [property: Key(3)] TradeType TradeType,
    /// <summary>Trade lifecycle status.</summary>
    [property: Key(4)] TradeStatus TradeStatus,
    /// <summary>Remaining days to expiry.</summary>
    [property: Key(5)] int DaysToExpiry
) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public TradePositionEntityId() : this(0, 0, default, TradeType.Unknown, default, 0) { }

    /// <summary>Returns a compact JSON representation.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    /// <summary>Hashes the dot-formatted identifier string.</summary>
    public override int GetHashCode() => $"{this}".GetHashCode();

    /// <summary>
    /// Creates a copy of this identifier with a different trade type.
    /// </summary>
    /// <param name="tradeType">Trade strategy/type.</param>
    public TradePositionEntityId FromTradeType(TradeType tradeType)
        => new(OrderId, TradeId, ValueDate, tradeType, TradeStatus, DaysToExpiry);

    /// <summary>
    /// Formats the identifier as a dot-separated string:
    /// "OrderId.TradeId.ValueDate.TradeType.TradeStatus.DaysToExpiry".
    /// </summary>
    public string Format()
        => $"{OrderId}.{TradeId}.{ValueDate:yyyy-MM-dd}.{TradeType}.{TradeStatus}.{DaysToExpiry}";
}