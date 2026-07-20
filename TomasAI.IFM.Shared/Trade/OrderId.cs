using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// MessagePack-serializable identifier for an order, composed of a single integer value.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot-separated components; with a single
/// component it resolves to "OrderId".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record OrderId(
    /// <summary>The unique numeric identifier of the order.</summary>
    [property: Key(0)] int Id) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to zero.
    /// </summary>
    public OrderId() : this(0) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    /// <returns>Formatted string (e.g., "12345").</returns>
    public string Format() => Id.ToString();
}
