using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a single trade fill data point for an option leg,
/// including pricing, commission, action, and audit metadata.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived members
/// are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeFillDataReadModel
{
    /// <summary>Parent order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Option contract identifier (symbol + maturity + type + strike).</summary>
    [Key(2)]
    public string ContractId { get; init; } = string.Empty;

    /// <summary>Timestamp when the fill occurred.</summary>
    [Key(3)]
    public DateTime FillDate { get; init; }

    /// <summary>Bid price recorded for the fill.</summary>
    [Key(4)]
    public decimal BidPrice { get; init; }

    /// <summary>Ask price recorded for the fill.</summary>
    [Key(5)]
    public decimal AskPrice { get; init; }

    /// <summary>Total commission charged for the fill.</summary>
    [Key(6)]
    public decimal Commission { get; init; }

    /// <summary>Indicates whether the leg is Short or Long.</summary>
    [Key(7)]
    public OptionLegAction OptionLegAction { get; init; }

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(8)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or system that created the record.</summary>
    [Key(9)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradeFillDataReadModel() { }

    /// <summary>
    /// Creates a new trade fill data view model.
    /// </summary>
    public TradeFillDataReadModel(
        int orderId,
        int tradeId,
        string contractId,
        DateTime fillDate,
        decimal bidPrice,
        decimal askPrice,
        decimal commission,
        OptionLegAction optionLegAction,
        DateTime createdOn,
        string createdBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ContractId = contractId ?? string.Empty;
        FillDate = fillDate;
        BidPrice = bidPrice;
        AskPrice = askPrice;
        Commission = commission;
        OptionLegAction = optionLegAction;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>Derived identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public TradeFillDataId Id => new(OrderId, TradeId, ContractId, FillDate);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0 && !string.IsNullOrEmpty(ContractId);

    /// <summary>Parsed option contract components (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public OptionContractId OptionContractId => OptionContractId.Create(ContractId);
}