using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Represents the unique identifier for a futures end-of-day data snapshot composed of a contract identifier
/// and a value date.
/// </summary>
/// <remarks>
/// MessagePack serializable (only primitive components are serialized). Provides helper methods for creation,
/// formatting, and JSON representation. Use <see cref="Create(string, DateOnly)"/> for explicit construction
/// when clarity is desired.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesDataId
    : IActorEntityId
{
    /// <summary>
    /// Full contract identifier (root + contract month/year code).
    /// </summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>
    /// The value date (trading/settlement date) associated with the data.
    /// </summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for some serializers and tooling scenarios.
    /// </summary>
    public FuturesDataId() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FuturesDataId"/> record.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    public FuturesDataId(string contractId, DateOnly valueDate)
    {
        ContractId = contractId;
        ValueDate = valueDate;
    }

    /// <summary>
    /// Factory method for creating a new <see cref="FuturesDataId"/> instance.
    /// </summary>
    /// <param name="contractId">The futures contract identifier.</param>
    /// <param name="valueDate">The value date.</param>
    /// <returns>A new <see cref="FuturesDataId"/>.</returns>
    public static FuturesDataId Create(string contractId, DateOnly valueDate) => new(contractId, valueDate);

    /// <summary>
    /// Formats the identifier into a stable string key representation.
    /// </summary>
    /// <returns>Formatted string (ContractId.ValueDate in yyyy-MM-dd).</returns>
    public string Format() => string.Create(null, stackalloc char[64], $"{ContractId}.{ValueDate:yyyy-MM-dd}");

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember]
    public bool IsValid
        => !string.IsNullOrWhiteSpace(ContractId) && ValueDate > DateOnly.MinValue;
}
