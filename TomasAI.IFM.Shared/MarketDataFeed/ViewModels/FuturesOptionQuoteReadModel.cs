using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// Represents a futures option quote registration containing the quote id, contract id,
/// request id, and audit metadata.
/// </summary>
/// <remarks>
/// MessagePack serializable. Only primitive members are serialized. The derived identifier
/// <see cref="Id"/> is excluded from serialization. Pattern aligns with FundOrderReadModel.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionQuoteReadModel
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

    /// <summary>User or system that created the quote mapping.</summary>
    [Key(3)]
    public string CreatedBy { get; init; }

    /// <summary>Timestamp when the quote mapping was created.</summary>
    [Key(4)]
    public DateTime CreatedOn { get; init; }

    /// <summary>
    /// Composite identifier for this quote mapping (not serialized).
    /// </summary>
    [JsonIgnore]
    [IgnoreMember]
    public FuturesOptionQuoteId Id => new(QuoteId, ContractId ?? string.Empty, RequestId);

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling.
    /// </summary>
    public FuturesOptionQuoteReadModel() { }

    /// <summary>
    /// Full constructor initializing all serialized properties.
    /// </summary>
    /// <param name="quoteId">Unique quote identifier.</param>
    /// <param name="contractId">Associated futures option contract identifier.</param>
    /// <param name="requestId">External/request identifier.</param>
    /// <param name="createdBy">Creator identity.</param>
    /// <param name="createdOn">Creation timestamp.</param>
    public FuturesOptionQuoteReadModel(
        int quoteId,
        string contractId,
        int requestId,
        string createdBy,
        DateTime createdOn)
    {
        QuoteId = quoteId;
        ContractId = contractId;
        RequestId = requestId;
        CreatedBy = createdBy;
        CreatedOn = createdOn;
    }

    /// <summary>
    /// Returns a compact JSON representation (diagnostics/logging).
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}