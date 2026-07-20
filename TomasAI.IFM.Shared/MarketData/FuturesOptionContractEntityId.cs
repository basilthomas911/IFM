using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketData;

/// <summary>
/// MessagePack-serializable identifier for a futures option contract entity (year-based).
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/> and formats to a stable dotted string. Since there is a single
/// component, the format resolves to "Year". If additional components are added later, they should be appended
/// using dot notation (e.g., "Year.Other").
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionContractEntityId(
    [property: Key(0)] string ContractId,
    [property: Key(1)] int Year)
    : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor required for some serializers; defaults to the current UTC year.
    /// </summary>
    public FuturesOptionContractEntityId() : this(string.Empty,DateTime.UtcNow.Year) { }

    /// <summary>
    /// Formats the identifier as a dot-separated key.
    /// </summary>
    /// <returns>Formatted string (e.g., "2025").</returns>
    public string Format() => $"{ContractId}.{Year}";

    [IgnoreMember]
    public bool IsValid => !string.IsNullOrWhiteSpace(ContractId) && Year > 0;
}

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
    public string Format() => Year.ToString();

    [IgnoreMember]
    public bool IsValid => Year > 0;
}
