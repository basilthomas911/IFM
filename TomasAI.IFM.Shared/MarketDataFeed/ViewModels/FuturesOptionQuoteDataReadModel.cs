using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents futures option quote data containing bid/ask price and size information
/// for a specific quote and contract.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive members are serialized. The derived
/// <see cref="IsValid"/> property is excluded from serialization.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteDataReadModel
{
    /// <summary>Unique quote identifier.</summary>
    [Key(0)]
    public int QuoteId { get; init; }

    /// <summary>Associated futures option contract identifier.</summary>
    [Key(1)]
    public string ContractId { get; init; }

    /// <summary>External/request identifier used when starting quote streams.</summary>
    [Key(2)]
    public int RequestId { get; init; }

    /// <summary>Bid price for the option quote.</summary>
    [Key(3)]
    public decimal BidPrice { get; init; }

    /// <summary>Bid size for the option quote.</summary>
    [Key(4)]
    public int BidSize { get; init; }

    /// <summary>Ask price for the option quote.</summary>
    [Key(5)]
    public decimal AskPrice { get; init; }

    /// <summary>Ask size for the option quote.</summary>
    [Key(6)]
    public int AskSize { get; init; }

    /// <summary>
    /// Indicates whether all bid/ask price and size values are valid (greater than zero).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid
        => default(bool) switch
        {
            _ when BidPrice <= 0m => false,
            _ when BidSize <= 0 => false,
            _ when AskPrice <= 0m => false,
            _ when AskSize <= 0 => false,
            _ => true
        };

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesOptionQuoteDataReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    /// <param name="quoteId">Unique quote identifier.</param>
    /// <param name="contractId">Associated futures option contract identifier.</param>
    /// <param name="requestId">External/request identifier.</param>
    /// <param name="bidPrice">Bid price for the option quote.</param>
    /// <param name="bidSize">Bid size for the option quote.</param>
    /// <param name="askPrice">Ask price for the option quote.</param>
    /// <param name="askSize">Ask size for the option quote.</param>
    public FuturesOptionQuoteDataReadModel(
        int quoteId,
        string contractId,
        int requestId,
        decimal bidPrice,
        int bidSize,
        decimal askPrice,
        int askSize)
    {
        QuoteId = quoteId;
        ContractId = contractId;
        RequestId = requestId;
        BidPrice = bidPrice;
        BidSize = bidSize;
        AskPrice = askPrice;
        AskSize = askSize;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}
