using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// MessagePack-serializable identifier for an option trade, composed of an OrderId and TradeId.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation: "OrderId.TradeId".
/// Provides a parameterless constructor (initializes to 0,0) and an <see cref="Empty"/> static instance
/// excluded from serialization.
/// </remarks>
/// <param name="OrderId">The parent order identifier.</param>
/// <param name="TradeId">The trade identifier within the order.</param>
[MessagePackObject(AllowPrivate = true)]
public record OptionTradeEntityId(
    [property: Key(0)] int OrderId,
    [property: Key(1)] int TradeId) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor required for some serializers; initializes to (0,0).
    /// </summary>
    public OptionTradeEntityId() : this(0, 0) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "OrderId.TradeId".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{OrderId}.{TradeId}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    /// <summary>
    /// Represents an empty (default) option trade identifier (0.0).
    /// </summary>
    [IgnoreMember]
    public static OptionTradeEntityId Empty => new(0, 0);

    [IgnoreMember]
    public bool IsValid
        => OrderId > 0 && TradeId > 0;
}
