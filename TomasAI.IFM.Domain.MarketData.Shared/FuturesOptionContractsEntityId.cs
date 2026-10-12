using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Shared;

[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractsEntityId(
    [property: Key(0)] int Year)
    : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor required for some serializers; defaults to the current UTC year.
    /// </summary>
    public FuturesOptionContractsEntityId() : this(DateTime.UtcNow.Year) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    /// <returns>Formatted string (e.g., "2025").</returns>
    public string Format() => Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [IgnoreMember]
    public bool IsValid => Year > 0;
}
