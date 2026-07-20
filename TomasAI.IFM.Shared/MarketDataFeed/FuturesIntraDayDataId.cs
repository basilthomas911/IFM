using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Represents a unique identifier for a specific set of futures intra-day data, defined by contract, value date, and
/// sequence number.
/// </summary>
/// <remarks>Use this record to uniquely identify and reference a particular futures intra-day data set. The
/// combination of contract ID, value date, and sequence ID ensures uniqueness across data points. Factory and
/// formatting methods are provided for convenience.</remarks>
/// <param name="ContractId">The identifier of the futures contract. Cannot be null or empty.</param>
/// <param name="ValueDate">The value date representing the end-of-day (EOD) for the data.</param>
/// <param name="SequenceId">The sequence number for the intra-day data. Defaults to 0 if not specified.</param>
[MessagePackObject(AllowPrivate = true)]
public record FuturesIntraDayDataId(
    /// <summary>The futures contract identifier.</summary>
    [property: Key(0)] string ContractId,
    /// <summary>The EOD as-of (value) date.</summary>
    [property: Key(1)] DateOnly ValueDate,
    /// <summary>The sequence identifier for intra-day data.</summary>
    [property: Key(2)] long SequenceId = 0) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public FuturesIntraDayDataId() : this(string.Empty, default) { }

    /// <summary>
    /// Creates a new instance of the FuturesIntraDayDataId class using the specified contract identifier, value date,
    /// and sequence number.
    /// </summary>
    /// <param name="contractId">The unique identifier of the futures contract. Cannot be null.</param>
    /// <param name="valueDate">The date for which the intra-day data applies.</param>
    /// <param name="sequenceId">The sequence number that specifies the order of the data entry.</param>
    /// <returns>A new FuturesIntraDayDataId instance initialized with the provided contract identifier, value date, and sequence
    /// number.</returns>
    public static FuturesIntraDayDataId Create(string contractId, DateOnly valueDate, long sequenceId)
        => new(contractId, valueDate, sequenceId);

    /// <summary>
    /// Formats the contract identifier, value date, and sequence identifier into a single string representation.
    /// </summary>
    public string Format()
        => $"{ContractId}.{ValueDate:yyyyMMdd}.{SequenceId}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}