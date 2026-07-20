using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Trade;

/// <summary>
/// MessagePack-serializable identifier for a trade placement composed of a contract identifier and a value date.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted representation uses dot-separated record parameters
/// (currently "ContractId.ValueDate" with ValueDate formatted as yyyyMMdd).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradePlacementId : IActorEntityId
{
    /// <summary>The futures/underlying contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; } = string.Empty;

    /// <summary>The as-of (value) date for the trade placement.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes properties to safe defaults.
    /// </summary>
    public TradePlacementId() { }

    /// <summary>
    /// MessagePack serialization constructor. Indices must match the <see cref="KeyAttribute"/> on properties.
    /// </summary>
    /// <param name="contractId">Contract identifier (Key 0).</param>
    /// <param name="valueDate">As-of (value) date (Key 1).</param>
    [SerializationConstructor]
    public TradePlacementId(string contractId, DateOnly valueDate)
    {
        ContractId = contractId ?? string.Empty;
        ValueDate = valueDate;
    }

    /// <summary>
    /// Formats the identifier using dot-separated record parameters: "ContractId.ValueDate".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{ContractId}.{ValueDate:yyyyMMdd}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
