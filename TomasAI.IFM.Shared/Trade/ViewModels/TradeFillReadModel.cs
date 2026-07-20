using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.TradeOrder;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a trade fill with its aggregated details
/// and associated per-leg fill data.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys,
/// derived members are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeFillReadModel
{
    /// <summary>Order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Timestamp of the fill.</summary>
    [Key(2)]
    public DateTime FillDate { get; init; }

    /// <summary>Total quantity filled.</summary>
    [Key(3)]
    public int FillQuantity { get; init; }

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(4)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or process that created the record.</summary>
    [Key(5)]
    public string CreatedBy { get; init; }

    /// <summary>Collection of per-leg fill details.</summary>
    [Key(6)]
    public TradeFillDataReadModel[] TradeFillData { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes string fields and arrays.
    /// </summary>
    public TradeFillReadModel()
    {
        CreatedBy = string.Empty;
        TradeFillData = [];
    }

    /// <summary>
    /// Creates a new trade fill view model.
    /// </summary>
    public TradeFillReadModel(
        int orderId,
        int tradeId,
        DateTime fillDate,
        int fillQuantity,
        DateTime createdOn,
        string createdBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        FillDate = fillDate;
        FillQuantity = fillQuantity;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
    }

    /// <summary>Derived identifier for the trade fill (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradeFillId Id => new(OrderId, TradeId, FillDate);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => OrderId > 0 && TradeId > 0;

    /// <summary>Derived trade order identifier (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradeOrderEntityId TradeOrderId => new(OrderId, TradeId, DateOnly.FromDateTime(FillDate));

    /// <summary>Computed average price across legs (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public decimal Price => (TradeFillData?.Length ?? 0) == 0
        ? 0
        : TradeFillData!.Sum(e => (e.BidPrice + e.AskPrice) / 2 * (e.OptionLegAction == OptionLegAction.Short ? -1 : 1));

    /// <summary>Total commission of all legs (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public decimal Commission => (TradeFillData?.Length ?? 0) == 0
        ? 0
        : TradeFillData!.Sum(e => e.Commission);

    /// <summary>
    /// Adds a collection of per-leg fill details to the trade fill.
    /// </summary>
    /// <param name="tradeFillData">Collection of leg details.</param>
    /// <returns>This instance for chaining.</returns>
    public TradeFillReadModel AddTradeFillData(ICollection<TradeFillDataReadModel> tradeFillData)
    {
        TradeFillData = [.. tradeFillData];
        return this;
    }

    /// <summary>
    /// Creates a deep copy of this TradeFillReadModel including its leg data.
    /// </summary>
    public TradeFillReadModel Copy()
        => new TradeFillReadModel(
            orderId: OrderId,
            tradeId: TradeId,
            fillDate: FillDate,
            fillQuantity: FillQuantity,
            createdOn: CreatedOn,
            createdBy: CreatedBy).AddTradeFillData(TradeFillData);
}