using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable model representing an option trade definition (metadata and identifiers).
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived/collection
/// members are excluded from MessagePack via IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record OptionTradeReadModel
{
    /// <summary>Parent order identifier.</summary>
    [Key(0)]
    public int OrderId { get; init; }

    /// <summary>Trade identifier within the order.</summary>
    [Key(1)]
    public int TradeId { get; init; }

    /// <summary>Strategy name/label for the option trade.</summary>
    [Key(2)]
    public string TradeStrategy { get; init; } 

    /// <summary>Trading (execution) date.</summary>
    [Key(3)]
    public DateOnly TradeDate { get; init; }

    /// <summary>Option maturity/expiry date.</summary>
    [Key(4)]
    public DateOnly MaturityDate { get; init; }

    /// <summary>Option trade strategy/type.</summary>
    [Key(5)]
    public TradeType TradeType { get; init; }

    /// <summary>Current trade lifecycle state.</summary>
    [Key(6)]
    public TradeState TradeState { get; init; }

    /// <summary>Trade action (Buy/Sell).</summary>
    [Key(7)]
    public TradeAction TradeAction { get; init; }

    /// <summary>Underlying contract identifier (e.g., futures symbol-month-year).</summary>
    [Key(8)]
    public string UnderlyingContractId { get; init; } = string.Empty;

    /// <summary>Underlying asset class.</summary>
    [Key(9)]
    public AssetType UnderlyingAssetType { get; init; }

    /// <summary>True if this trade is the primary trade in the set.</summary>
    [Key(10)]
    public bool IsPrimaryTrade { get; init; }

    /// <summary>True if this trade is a hedge trade.</summary>
    [Key(11)]
    public bool IsHedgeTrade { get; init; }

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(12)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or system that created the record.</summary>
    [Key(13)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Last updated timestamp (UTC preferred).</summary>
    [Key(14)]
    public DateTime UpdatedOn { get; init; }

    /// <summary>User or system that last updated the record.</summary>
    [Key(15)]
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>
    /// Parameterless constructor for serializers; initializes strings and timestamps.
    /// </summary>
    public OptionTradeReadModel()
    {
        OrderId = 0;
        TradeId = 0;
        TradeStrategy = string.Empty;
        UnderlyingContractId = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
        CreatedOn = DateTime.UtcNow;
        UpdatedOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new option trade data model.
    /// </summary>
    public OptionTradeReadModel(
        int orderId,
        int tradeId,
        string tradeStrategy,
        DateOnly tradeDate,
        DateOnly maturityDate,
        TradeType tradeType,
        TradeState tradeState,
        TradeAction tradeAction,
        string underlyingContractId,
        AssetType underlyingAssetType,
        bool isPrimaryTrade,
        bool isHedgeTrade,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    {
        OrderId = orderId;
        TradeId = tradeId;
        TradeStrategy = tradeStrategy ?? string.Empty;
        TradeDate = tradeDate;
        MaturityDate = maturityDate;
        TradeType = tradeType;
        TradeState = tradeState;
        TradeAction = tradeAction;
        UnderlyingContractId = underlyingContractId ?? string.Empty;
        UnderlyingAssetType = underlyingAssetType;
        IsPrimaryTrade = isPrimaryTrade;
        IsHedgeTrade = isHedgeTrade;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>Derived option trade identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public OptionTradeEntityId EntityId => new(OrderId, TradeId);

    /// <summary>Option legs (JSON only; excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public OptionTradeLegReadModel[]? OptionLegs { get; private set; }

    /// <summary>Trade positions (JSON only; excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradePositionReadModel[]? TradePositions { get; private set; }

    /// <summary>Trade limit (JSON only; excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradeLimitReadModel? TradeLimit { get; private set; }

    /// <summary>Trade type limits (JSON only; excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradeTypeLimitReadModel[]? TradeTypeLimits { get; private set; }

    /// <summary>Trade fills (JSON only; excluded from MessagePack).</summary>
    [JsonProperty]
    [IgnoreMember]
    public TradeFillReadModel[]? TradeFills { get; private set; }

    /// <summary>
    /// Adds option legs to the trade.
    /// </summary>
    public OptionTradeReadModel AddOptionLegs(ICollection<OptionTradeLegReadModel> optionLegs)
    {
        OptionLegs = [.. optionLegs];
        return this;
    }

    /// <summary>
    /// Adds trade positions to the trade.
    /// </summary>
    public OptionTradeReadModel AddTradePosition(ICollection<TradePositionReadModel> tradePositions)
    {
        TradePositions = [.. tradePositions];
        return this;
    }

    /// <summary>
    /// Sets the trade limit object.
    /// </summary>
    public OptionTradeReadModel SetTradeLimit(TradeLimitReadModel tradeLimit)
    {
        TradeLimit = tradeLimit;
        return this;
    }

    /// <summary>
    /// Sets trade limit values (max loss and profit targets).
    /// </summary>
    public OptionTradeReadModel SetTradeLimit(decimal maxLoss, decimal minProfitTarget, decimal dailyProfitTarget)
    {
        if (TradeLimit is not null)
            TradeLimit = TradeLimit with
            {
                MaxLoss = maxLoss,
                MinProfitTarget = minProfitTarget,
                DailyProfitTarget = dailyProfitTarget
            };
        return this;
    }

    /// <summary>
    /// Sets detailed trade limit values including margins and limits.
    /// </summary>
    public OptionTradeReadModel SetTradeLimit(
        decimal riskMargin,
        decimal maxProfit,
        decimal maxLoss,
        decimal maxReturn,
        decimal maxLossLimit,
        decimal minProfitLimit,
        decimal maxProfitLimit,
        decimal minProfitTarget,
        decimal dailyProfitTarget)
    {
        if (TradeLimit is not null)
            TradeLimit = TradeLimit with
            {
                RiskMargin = riskMargin,
                MaxProfit = maxProfit,
                MaxLoss = maxLoss,
                MaxReturn = maxReturn,
                MaxLossLimit = maxLossLimit,
                MinProfitLimit = minProfitLimit,
                MaxProfitLimit = maxProfitLimit,
                MinProfitTarget = minProfitTarget,
                DailyProfitTarget = dailyProfitTarget
            };
        return this;
    }

    /// <summary>
    /// Sets the risk margin on the trade limit.
    /// </summary>
    public OptionTradeReadModel SetRiskMargin(decimal riskMargin)
    {
        if (TradeLimit is not null)
            TradeLimit = TradeLimit with { RiskMargin = riskMargin };
        return this;
    }

    /// <summary>
    /// Adds trade type limits.
    /// </summary>
    public OptionTradeReadModel AddTradeTypeLimits(ICollection<TradeTypeLimitReadModel> tradeTypeLimits)
    {
        TradeTypeLimits = [.. tradeTypeLimits];
        return this;
    }

    /// <summary>
    /// Adds trade fills.
    /// </summary>
    public OptionTradeReadModel AddTradeFills(ICollection<TradeFillReadModel> tradeFills)
    {
        TradeFills = [.. tradeFills];
        return this;
    }

    /// <summary>
    /// Returns a compact JSON representation of the model.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);

    [IgnoreMember]
    public bool IsValid 
        => EntityId.IsValid;
}