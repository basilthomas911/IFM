using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// MessagePack-serializable identifier for end-of-day (EOD) futures data, composed of a contract id and a value date.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation "ContractId.ValueDate" with ValueDate in yyyyMMdd.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesEodDataId(
    /// <summary>The futures contract identifier.</summary>
    [property: Key(0)] string ContractId,
    /// <summary>The EOD as-of (value) date.</summary>
    [property: Key(1)] DateOnly ValueDate) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public FuturesEodDataId() : this(string.Empty, default) { }

    /// <summary>
    /// Factory method to create a new futures EOD data identifier.
    /// </summary>
    public static FuturesEodDataId Create(string contractId, DateOnly valueDate) => new(contractId, valueDate);

    /// <summary>
    /// Formats the identifier as a dot-separated string: "ContractId.ValueDate".
    /// </summary>
    public string Format()
        => $"{ContractId}.{ValueDate:yyyyMMdd}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}