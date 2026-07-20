using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.PredictiveModel.FuturesItiTrend;

/// <summary>
/// MessagePack-serializable identifier for a Futures ITI Trend entity, composed of a symbol and a value date.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation: "Symbol.ValueDate" (yyyy-MM-dd).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiTrendEntityId(
    [property: Key(0)] string Symbol,
    [property: Key(1)] DateOnly ValueDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to empty/defaults.
    /// </summary>
    public FuturesItiTrendEntityId() : this(string.Empty, default) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "Symbol.ValueDate".
    /// </summary>
    /// <returns>Formatted string with ValueDate in yyyy-MM-dd format.</returns>
    public string Format() => string.Create(null, stackalloc char[64], $"{Symbol}.{ValueDate:yyyy-MM-dd}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
