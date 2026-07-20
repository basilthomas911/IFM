using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// MessagePack-serializable identifier for a futures tick data record, composed of ContractId, ValueDate, and TickId.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "ContractId.ValueDate.TickId" with ValueDate as yyyyMMdd.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesTickDataId(
    /// <summary>The futures contract identifier.</summary>
    [property: Key(0)] string ContractId,
    /// <summary>The as-of (value) date for the tick.</summary>
    [property: Key(1)] DateOnly ValueDate,
    /// <summary>Monotonic tick identifier within the value date.</summary>
    [property: Key(2)] long TickId) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public FuturesTickDataId() : this(string.Empty, default, 0L) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string.
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{ContractId}.{ValueDate:yyyyMMdd}.{TickId}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    public override int GetHashCode() => $"{this}".GetHashCode();
}
