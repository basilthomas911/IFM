using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData;

/// <summary>
/// MessagePack-serializable identifier for a yield curve rate entity (year-based).
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. Formatting uses dot-separated components; with a single component
/// it resolves to "Year".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record YieldCurveRateEntityId(
    [property: Key(0)]
    int Year) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; defaults to current UTC year.
    /// </summary>
    public YieldCurveRateEntityId() : this(DateTime.UtcNow.Year) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    public string Format() => Year.ToString();
}
