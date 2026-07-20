using MessagePack;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a trade's limit configuration,
/// including risk margin, profit/loss thresholds, targets, and audit metadata.
/// </summary>
/// <remarks>
/// Pattern mirrors TradePlanForwardLossLimitReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record TradeLimitReadModel
{
    /// <summary>Trade identifier.</summary>
    [Key(0)]
    public int TradeId { get; init; }

    /// <summary>Trade strategy/type.</summary>
    [Key(1)]
    public TradeType TradeType { get; init; }

    /// <summary>Risk margin used for risk management calculations.</summary>
    [Key(2)]
    public decimal RiskMargin { get; init; }

    /// <summary>Maximum expected profit for the trade.</summary>
    [Key(3)]
    public decimal MaxProfit { get; init; }

    /// <summary>Maximum expected loss for the trade.</summary>
    [Key(4)]
    public decimal MaxLoss { get; init; }

    /// <summary>Maximum expected return for the trade.</summary>
    [Key(5)]
    public decimal MaxReturn { get; init; }

    /// <summary>Maximum allowed loss limit threshold.</summary>
    [Key(6)]
    public decimal MaxLossLimit { get; init; }

    /// <summary>Minimum profit limit threshold.</summary>
    [Key(7)]
    public decimal MinProfitLimit { get; init; }

    /// <summary>Maximum profit limit threshold.</summary>
    [Key(8)]
    public decimal MaxProfitLimit { get; init; }

    /// <summary>Minimum profit target.</summary>
    [Key(9)]
    public decimal MinProfitTarget { get; init; }

    /// <summary>Daily profit target.</summary>
    [Key(10)]
    public decimal DailyProfitTarget { get; init; }

    /// <summary>Creation timestamp.</summary>
    [Key(11)]
    public DateTime CreatedOn { get; init; }

    /// <summary>Creator identity.</summary>
    [Key(12)]
    public string CreatedBy { get; init; } = string.Empty;

    /// <summary>Last update timestamp.</summary>
    [Key(13)]
    public DateTime UpdatedOn { get; init; }

    /// <summary>Last updater identity.</summary>
    [Key(14)]
    public string UpdatedBy { get; init; } = string.Empty;

    /// <summary>Parameterless constructor for serializers.</summary>
    public TradeLimitReadModel() { }

    /// <summary>
    /// Full constructor to initialize a trade limit configuration.
    /// </summary>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="tradeType">Trade strategy/type.</param>
    /// <param name="riskMargin">Risk margin used for risk calculations.</param>
    /// <param name="maxProfit">Maximum expected profit.</param>
    /// <param name="maxLoss">Maximum expected loss.</param>
    /// <param name="maxReturn">Maximum expected return.</param>
    /// <param name="maxLossLimit">Maximum allowed loss limit threshold.</param>
    /// <param name="minProfitLimit">Minimum profit limit threshold.</param>
    /// <param name="maxProfitLimit">Maximum profit limit threshold.</param>
    /// <param name="minProfitTarget">Minimum profit target.</param>
    /// <param name="dailyProfitTarget">Daily profit target.</param>
    /// <param name="createdOn">Creation timestamp.</param>
    /// <param name="createdBy">Creator identity.</param>
    /// <param name="updatedOn">Last update timestamp.</param>
    /// <param name="updatedBy">Last updater identity.</param>
    public TradeLimitReadModel(
        int tradeId,
        TradeType tradeType,
        decimal riskMargin,
        decimal maxProfit,
        decimal maxLoss,
        decimal maxReturn,
        decimal maxLossLimit,
        decimal minProfitLimit,
        decimal maxProfitLimit,
        decimal minProfitTarget,
        decimal dailyProfitTarget,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    {
        TradeId = tradeId;
        TradeType = tradeType;
        RiskMargin = riskMargin;
        MaxProfit = maxProfit;
        MaxLoss = maxLoss;
        MaxReturn = maxReturn;
        MaxLossLimit = maxLossLimit;
        MinProfitLimit = minProfitLimit;
        MaxProfitLimit = maxProfitLimit;
        MinProfitTarget = minProfitTarget;
        DailyProfitTarget = dailyProfitTarget;
        CreatedOn = createdOn;
        CreatedBy = createdBy ?? string.Empty;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy ?? string.Empty;
    }

    /// <summary>
    /// Creates a default trade limit configuration with zeroed monetary fields and empty audit metadata.
    /// </summary>
    /// <param name="tradeId">Trade identifier.</param>
    /// <param name="tradeType">Trade strategy/type.</param>
    [IgnoreMember]
    public bool IsValid => TradeId > 0;

    public static TradeLimitReadModel Default(int tradeId, TradeType tradeType)
        => new(
            tradeId: tradeId,
            tradeType: tradeType,
            riskMargin: 0m,
            maxProfit: 0m,
            maxLoss: 0m,
            maxReturn: 0m,
            maxLossLimit: 0m,
            minProfitLimit: 0m,
            maxProfitLimit: 0m,
            minProfitTarget: 0m,
            dailyProfitTarget: 0m,
            createdOn: DateTime.MinValue,
            createdBy: string.Empty,
            updatedOn: DateTime.MinValue,
            updatedBy: string.Empty);
}