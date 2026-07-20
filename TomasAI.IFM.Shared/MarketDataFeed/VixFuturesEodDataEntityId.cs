using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Unique identifier for a VIX futures end-of-day (EOD) data snapshot composed of a contract identifier
/// and a value (trading) date.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot-separated
/// key format: ContractId.yyyy-MM-dd. Provides factory, formatting, and JSON helpers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record VixFuturesEodDataEntityId : IActorEntityId
{
    /// <summary>Full VIX futures contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading (value) date for the EOD snapshot.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public VixFuturesEodDataEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="VixFuturesEodDataEntityId"/>.
    /// </summary>
    /// <param name="contractId">VIX futures contract identifier.</param>
    /// <param name="valueDate">Trading (value) date.</param>
    public VixFuturesEodDataEntityId(string contractId, DateOnly valueDate)
    {
        ContractId = contractId;
        ValueDate = valueDate;
    }

    /// <summary>
    /// Factory method for explicit creation.
    /// </summary>
    public static VixFuturesEodDataEntityId Create(string contractId, DateOnly valueDate)
        => new(contractId, valueDate);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyy-MM-dd
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{ContractId}.{ValueDate:yyyy-MM-dd}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
