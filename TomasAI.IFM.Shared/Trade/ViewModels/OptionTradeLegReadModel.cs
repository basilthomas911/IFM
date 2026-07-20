using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a single option leg attached to an order/trade.
/// </summary>
/// <remarks>
/// Pattern follows other MessagePack-compatible view models in the project:
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers.
/// - A full constructor annotated with <see cref="SerializationConstructorAttribute"/> for deserialization.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record OptionTradeLegReadModel
{
    /// <summary>Parent order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Futures contract identifier for this leg.</summary>
    [Key(2)]
    public string ContractId { get; init; } = string.Empty;

    /// <summary>Quantity (positive for long, negative for short depending on <see cref="OptionLegAction"/>).</summary>
    [Key(3)]
    public int Quantity { get; init; }

    /// <summary>Strike price for the option contract.</summary>
    [Key(4)]
    public decimal StrikePrice { get; init; }

    /// <summary>Option type (Put/Call).</summary>
    [Key(5)]
    public OptionType OptionLegType { get; init; }

    /// <summary>Action for the leg (Short/Long).</summary>
    [Key(6)]
    public OptionLegAction OptionLegAction { get; init; }

    /// <summary>Record creation timestamp.</summary>
    [Key(7)]
    public DateTime CreatedOn { get; init; }

    /// <summary>Record creator identity.</summary>
    [Key(8)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Last update timestamp.</summary>
    [Key(9)]
    public DateTime UpdatedOn { get; init; }

    /// <summary>Last updater identity.</summary>
    [Key(10)]
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>Derived identifier composed from <see cref="OrderId"/>, <see cref="TradeId"/> and <see cref="ContractId"/>.</summary>
    [JsonIgnore]
    [IgnoreMember]
    public OptionLegId Id => new(OrderId, TradeId, ContractId);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0 && !string.IsNullOrEmpty(ContractId);

    /// <summary>Parameterless constructor for serializers.</summary>
    public OptionTradeLegReadModel() { }

    /// <summary>
    /// MessagePack serialization constructor. Indices must match the <see cref="KeyAttribute"/> annotations.
    /// </summary>
    [SerializationConstructor]
    public OptionTradeLegReadModel(
        int orderId,                    // Key(0)
        int tradeId,                    // Key(1)
        string contractId,              // Key(2)
        int quantity,                   // Key(3)
        decimal strikePrice,            // Key(4)
        OptionType optionLegType,       // Key(5)
        OptionLegAction optionLegAction,// Key(6)
        DateTime createdOn,             // Key(7)
        string createdBy,               // Key(8)
        DateTime updatedOn,             // Key(9)
        string updatedBy)               // Key(10)
    {
        OrderId = orderId;
        TradeId = tradeId;
        ContractId = contractId ?? string.Empty;
        Quantity = quantity;
        StrikePrice = strikePrice;
        OptionLegType = optionLegType;
        OptionLegAction = optionLegAction;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>
    /// Creates a default OptionLegReadModel with zeroed metrics and empty metadata.
    /// </summary>
    public static OptionTradeLegReadModel Default(int orderId, int tradeId, string contractId, OptionType optionType, OptionLegAction optionLegAction)
        => new (
            orderId: orderId,
            tradeId: tradeId,
            contractId: contractId,
            quantity: 0,
            strikePrice: 0,
            optionLegType: optionType,
            optionLegAction: optionLegAction,
            createdOn: DateTime.Now,
            createdBy: string.Empty,
            updatedOn: DateTime.Now,
            updatedBy: string.Empty);

    /// <summary>
    /// Creates a copy of this option leg snapshot. <see cref="UpdatedOn"/> is set to <see cref="DateTime.Now"/>.
    /// </summary>
    public OptionTradeLegReadModel Copy()
        => new (
            orderId: this.OrderId,
            tradeId: this.TradeId,
            contractId: this.ContractId,
            quantity: this.Quantity,
            strikePrice: this.StrikePrice,
            optionLegType: this.OptionLegType,
            optionLegAction: this.OptionLegAction,
            createdOn: this.CreatedOn,
            createdBy: this.CreatedBy,
            updatedOn: DateTime.Now,
            updatedBy: this.UpdatedBy);
}