using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.OptionPricer;

/// <summary>
/// MessagePack-serializable identifier for a spread distribution snapshot, composed of TradeId and ValueDate.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation: "TradeId.ValueDate" (yyyy-MM-dd).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record SpreadDistributionEntityId(
    [property: Key(0)] int TradeId,
    [property: Key(1)] DateOnly ValueDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public SpreadDistributionEntityId() : this(0, default) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "TradeId.ValueDate".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{TradeId}.{ValueDate:yyyy-MM-dd}");
}
