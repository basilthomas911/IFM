using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.MarketDataFeed;

/// <summary>
/// Unique identifier for a futures option quote composed of:
/// QuoteId, ContractId, and RequestId.
/// </summary>
/// <remarks>
/// MessagePack serializable. Implements <see cref="IActorEntityId"/> with dot-separated key format:
/// QuoteId.ContractId.RequestId. Provides JSON string representation for diagnostics.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteId : IActorEntityId
{
    /// <summary>Unique quote identifier.</summary>
    [Key(0)]
    public int QuoteId { get; init; }

    /// <summary>Associated futures option contract identifier.</summary>
    [Key(1)]
    public string ContractId { get; init; }

    /// <summary>External/request identifier for the quote stream.</summary>
    [Key(2)]
    public int RequestId { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesOptionQuoteId() { }

    /// <summary>
    /// Initializes a new <see cref="FuturesOptionQuoteId"/>.
    /// </summary>
    public FuturesOptionQuoteId(int quoteId, string contractId, int requestId)
    {
        QuoteId = quoteId;
        ContractId = contractId;
        RequestId = requestId;
    }

    /// <summary>
    /// Factory helper for explicit creation.
    /// </summary>
    public static FuturesOptionQuoteId Create(int quoteId, string contractId, int requestId)
        => new(quoteId, contractId, requestId);

    /// <summary>
    /// Formats the identifier into a stable string key: QuoteId.ContractId.RequestId.
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[80], $"{QuoteId}.{ContractId}.{RequestId}");

    /// <summary>
    /// Returns a compact JSON representation.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
