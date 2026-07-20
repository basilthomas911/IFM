using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.TradeOrder.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing an option trade order, including identifiers,
/// dates, state, pricing, amounts, and metadata. Collections (legs, fills, limits) are exposed for JSON only.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys;
/// derived/collection members are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeOrderReadModel
{
    [Key(0)]
    public int FundId { get; init; }
    [Key(1)]
    public int OrderId { get; init; }
    [Key(2)]
    public int TradeId { get; init; }
    [Key(3)]
    public DateOnly ValueDate { get; init; }
    [Key(4)]
    public TradeType TradeType { get; init; }
    [Key(5)]
    public TradeSubType TradeSubType { get; init; }
    [Key(6)]
    public DateOnly TradeDate { get; init; }
    [Key(7)]
    public DateOnly MaturityDate { get; init; }
    [Key(8)]
    public TradeOrderState TradeOrderState { get; init; }
    [Key(9)]
    public string UnderlyingContractId { get; init; } = string.Empty;
    [Key(10)]
    public AssetType UnderlyingAssetType { get; init; }
    [Key(11)]
    public string OrderDescription { get; init; } = string.Empty;
    [Key(12)]
    public OrderAction OrderAction { get; init; }
    [Key(13)]
    public OrderActionType OrderActionType { get; init; }
    [Key(14)]
    public int OrderQuantity { get; init; }
    [Key(15)]
    public int OrderFilled { get; init; }
    [Key(16)]
    public OrderType OrderType { get; init; }
    [Key(17)]
    public decimal OrderPrice { get; init; }
    [Key(18)]
    public decimal OrderAmount { get; init; }
    [Key(19)]
    public decimal Commission { get; init; }
    [Key(20)]
    public decimal TotalAmount { get; init; }
    [Key(21)]
    public decimal TradePnl { get; init; }
    [Key(22)]
    public TradeFillType TradeFillType { get; init; }
    [Key(23)]
    public DateTime CreatedOn { get; init; }
    [Key(24)]
    public string CreatedBy { get; init; } = string.Empty;
    [Key(25)]
    public DateTime UpdatedOn { get; init; }
    [Key(26)]
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor for serializers; initializes strings to empty and timestamps to UTC now.
    /// </summary>
    public TradeOrderReadModel()
    {
        UnderlyingContractId = string.Empty;
        OrderDescription = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
        CreatedOn = DateTime.UtcNow;
        UpdatedOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Full constructor to initialize a trade order view model.
    /// </summary>
    public TradeOrderReadModel(
        int fundId,
        int orderId,
        int tradeId,
        DateOnly valueDate,
        TradeType tradeType,
        TradeSubType tradeSubType,
        DateOnly tradeDate,
        DateOnly maturityDate,
        TradeOrderState tradeOrderState,
        string underlyingContractId,
        AssetType underlyingAssetType,
        string orderDescription,
        OrderAction orderAction,
        OrderActionType orderActionType,
        int orderQuantity,
        int orderFilled,
        OrderType orderType,
        decimal orderPrice,
        decimal orderAmount,
        decimal commission,
        decimal totalAmount,
        decimal tradePnl,
        TradeFillType tradeFillType,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    {
        FundId = fundId;
        OrderId = orderId;
        TradeId = tradeId;
        ValueDate = valueDate;
        TradeType = tradeType;
        TradeSubType = tradeSubType;
        TradeDate = tradeDate;
        MaturityDate = maturityDate;
        TradeOrderState = tradeOrderState;
        UnderlyingContractId = underlyingContractId ?? string.Empty;
        UnderlyingAssetType = underlyingAssetType;
        OrderDescription = orderDescription ?? string.Empty;
        OrderAction = orderAction;
        OrderActionType = orderActionType;
        OrderQuantity = orderQuantity;
        OrderFilled = orderFilled;
        OrderType = orderType;
        OrderPrice = orderPrice;
        OrderAmount = orderAmount;
        Commission = commission;
        TotalAmount = totalAmount;
        TradePnl = tradePnl;
        TradeFillType = tradeFillType;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    // Backing collections (not serialized by MessagePack)
    [JsonIgnore]
    [IgnoreMember]
    private List<OptionTradeLegReadModel>? _optionLegs = [];
    [JsonIgnore]
    [IgnoreMember]
    private TradeLimitReadModel _tradeLimit;
    [JsonIgnore]
    [IgnoreMember]
    private List<TradeTypeLimitReadModel>? _tradeTypeLimits = [];
    [JsonIgnore]
    [IgnoreMember]
    private List<TradeFillReadModel>? _tradeFills = [];

    /// <summary>Computed trade order entity identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public TradeOrderEntityId EntityId => new(OrderId, TradeId, ValueDate);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => FundId > 0 && OrderId > 0 && TradeId > 0;

    /// <summary>Project legs as an array for JSON (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public OptionTradeLegReadModel[] OptionLegs => _optionLegs is null ? [] : [.. _optionLegs];

    /// <summary>Trade limit projection for JSON (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradeLimitReadModel TradeLimit => _tradeLimit;

    /// <summary>Trade type limits projection for JSON (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradeTypeLimitReadModel[] TradeTypeLimits => _tradeTypeLimits is null ? [] : [.. _tradeTypeLimits];

    /// <summary>Trade fills projection for JSON (excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradeFillReadModel[] TradeFills => _tradeFills is null ? [] : [.. _tradeFills];

    /// <summary>Total filled quantity based on fills (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public int FilledQuantity => _tradeFills?.Sum(e => e.FillQuantity) ?? 0;

    /// <summary>Adds option legs.</summary>
    public TradeOrderReadModel AddOptionLegs(ICollection<OptionTradeLegReadModel> optionLegs)
    {
        _optionLegs ??= [];
        _optionLegs.AddRange(optionLegs);
        return this;
    }

    /// <summary>Sets the trade limit object.</summary>
    public TradeOrderReadModel SetTradeLimit(TradeLimitReadModel tradeLimit)
    {
        _tradeLimit = tradeLimit;
        return this;
    }

    /// <summary>Sets trade limit values (max loss and profit targets).</summary>
    public TradeOrderReadModel SetTradeLimit(decimal maxLoss, decimal minProfitTarget, decimal dailyProfitTarget)
    {
        _tradeLimit = this.TradeLimit with
        {
            MaxLoss = maxLoss,
            MinProfitTarget = minProfitTarget,
            DailyProfitTarget = dailyProfitTarget
        };
        return this;
    }

    /// <summary>Sets detailed trade limit values.</summary>
    public TradeOrderReadModel SetTradeLimit(
        decimal riskMargin,
        decimal maxProfit,
        decimal maxLoss,
        decimal maxReturn,
        decimal maxLossLimit,
        decimal minProfitLimit,
        decimal minProfitTarget,
        decimal dailyProfitTarget)
    {
        _tradeLimit = this.TradeLimit with
        {
            RiskMargin = riskMargin,
            MaxProfit = maxProfit,
            MaxLoss = maxLoss,
            MaxReturn = maxReturn,
            MaxLossLimit = maxLossLimit,
            MinProfitLimit = minProfitLimit,
            MinProfitTarget = minProfitTarget,
            DailyProfitTarget = dailyProfitTarget
        };
        return this;
    }

    /// <summary>Adds trade type limits.</summary>
    public TradeOrderReadModel AddTradeTypeLimits(ICollection<TradeTypeLimitReadModel> tradeTypeLimits)
    {
        _tradeTypeLimits ??= [];
        _tradeTypeLimits.AddRange(tradeTypeLimits);
        return this;
    }

    /// <summary>Adds a single trade fill.</summary>
    public TradeOrderReadModel AddTradeFill(TradeFillReadModel tradeFill)
    {
        _tradeFills ??= [];
        _tradeFills.Add(tradeFill);
        return this;
    }

    /// <summary>Adds multiple trade fills.</summary>
    public TradeOrderReadModel AddTradeFills(ICollection<TradeFillReadModel> tradeFills)
    {
        _tradeFills ??= [];
        _tradeFills.AddRange(tradeFills);
        return this;
    }

    /// <summary>Converts order action to trade action.</summary>
    public TradeAction GetTradeAction()
        => OrderAction switch
        {
            OrderAction.Buy => TradeAction.Buy,
            OrderAction.Sell => TradeAction.Sell,
            _ => default
        };

    /// <summary>Returns a compact JSON representation of the model.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}