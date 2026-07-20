using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Unique identifier for a futures option tick entity composed of a contract identifier
/// and a value (trading) date.
/// </summary>
/// <remarks>
/// MessagePack serializable (primitive components only). Implements <see cref="IActorEntityId"/> with a dot-separated
/// key format: ContractId.yyyy-MM-dd. Includes a factory method overload that accepts a tickId for call-site compatibility
/// even though the identifier semantics are contract + value date.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionTickEntityId : IActorEntityId
{
    /// <summary>Full futures option contract identifier.</summary>
    [Key(0)]
    public string ContractId { get; init; }

    /// <summary>Trading (value) date.</summary>
    [Key(1)]
    public DateOnly ValueDate { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and some serializers.
    /// </summary>
    public FuturesOptionTickEntityId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesOptionTickEntityId"/>.
    /// </summary>
    /// <param name="contractId">Futures option contract identifier.</param>
    /// <param name="valueDate">Trading (value) date.</param>
    public FuturesOptionTickEntityId(string contractId, DateOnly valueDate)
    {
        ContractId = contractId;
        ValueDate = valueDate;
    }

    /// <summary>
    /// Factory method for explicit creation. The <paramref name="tickId"/> parameter is accepted for call-site compatibility
    /// but is not part of the identifier and is ignored.
    /// </summary>
    /// <param name="contractId">Futures option contract identifier.</param>
    /// <param name="valueDate">Trading (value) date.</param>
    /// <param name="tickId">Monotonic tick identifier (ignored for identity).</param>
    public static FuturesOptionTickEntityId Create(string contractId, DateOnly valueDate, long tickId)
        => new(contractId, valueDate);

    /// <summary>
    /// Formats the identifier into a stable string key: ContractId.yyyy-MM-dd
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{ContractId}.{ValueDate:yyyyMMdd}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);

    [IgnoreMember]
    public bool IsValid
        => !string.IsNullOrEmpty(ContractId) && ValueDate != default;
}
