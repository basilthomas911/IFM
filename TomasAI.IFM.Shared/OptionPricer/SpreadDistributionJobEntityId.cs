using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.OptionPricer;

/// <summary>
/// MessagePack-serializable identifier for a spread distribution job, composed of OrderId and TradeId.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. Uses dot-separated format "OrderId.TradeId".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record struct SpreadDistributionJobEntityId(
    /// <summary>The order identifier.</summary>
    [property: Key(0)] int OrderId,
    /// <summary>The trade identifier within the order.</summary>
    [property: Key(1)] int TradeId,
    [property: Key(2)] DateOnly ValueDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public SpreadDistributionJobEntityId() : this(0, 0, DateOnly.MinValue) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "OrderId.TradeId.ValueDate".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[32], $"{OrderId}.{TradeId}.{ValueDate:yyyyMMdd}");

    [IgnoreMember]
    public bool IsValid 
        => OrderId > 0 && TradeId > 0 && ValueDate > DateOnly.MinValue;
}