using MessagePack;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

/// <summary>Captured legacy business values for one Iron Condor monitoring revision. Null means unavailable, never zero.</summary>
[MessagePackObject]
public sealed partial record IronCondorTradePlanSnapshot
{
    // Removed member keys 14,15,21-26,28-30,35,40-47,50 stay reserved. Never renumber/reuse persisted MessagePack keys.
    /// <summary>Gets the legacy OrderId value; null indicates that its required source has not been captured.</summary>
    [Key(0)] public int? OrderId { get; init; }
    /// <summary>Gets the legacy TradeId value; null indicates that its required source has not been captured.</summary>
    [Key(1)] public int? TradeId { get; init; }
    /// <summary>Gets the legacy ValueDate value; null indicates that its required source has not been captured.</summary>
    [Key(2)] public DateOnly? ValueDate { get; init; }
    /// <summary>Gets the legacy SequenceId value; null indicates that its required source has not been captured.</summary>
    [Key(3)] public long? SequenceId { get; init; }
    /// <summary>Gets the legacy ActionDate value; null indicates that its required source has not been captured.</summary>
    [Key(4)] public DateTime? ActionDate { get; init; }
    /// <summary>Gets the legacy TradeDate value; null indicates that its required source has not been captured.</summary>
    [Key(5)] public DateOnly? TradeDate { get; init; }
    /// <summary>Gets the legacy MaturityDate value; null indicates that its required source has not been captured.</summary>
    [Key(6)] public DateOnly? MaturityDate { get; init; }
    /// <summary>Gets the legacy TradeType value; null indicates that its required source has not been captured.</summary>
    [Key(7)] public string? TradeType { get; init; }
    /// <summary>Gets the legacy ActionType value; null indicates that its required source has not been captured.</summary>
    [Key(8)] public string? ActionType { get; init; }
    /// <summary>Gets the legacy ActionSubType value; null indicates that its required source has not been captured.</summary>
    [Key(9)] public string? ActionSubType { get; init; }
    /// <summary>Gets the legacy ActionState value; null indicates that its required source has not been captured.</summary>
    [Key(10)] public string? ActionState { get; init; }
    /// <summary>Gets the legacy ActionReason value; null indicates that its required source has not been captured.</summary>
    [Key(11)] public string? ActionReason { get; init; }
    /// <summary>Gets the legacy TradePnl value; null indicates that its required source has not been captured.</summary>
    [Key(12)] public decimal? TradePnl { get; init; }
    /// <summary>Gets the legacy ForwardLossRatio value; null indicates that its required source has not been captured.</summary>
    [Key(13)] public double? ForwardLossRatio { get; init; }
    /// <summary>Gets the legacy MaxProfit value; null indicates that its required source has not been captured.</summary>
    [Key(16)] public decimal? MaxProfit { get; init; }
    /// <summary>Gets the legacy MaxLoss value; null indicates that its required source has not been captured.</summary>
    [Key(17)] public decimal? MaxLoss { get; init; }
    /// <summary>Gets the legacy MinProfitTarget value; null indicates that its required source has not been captured.</summary>
    [Key(18)] public decimal? MinProfitTarget { get; init; }
    /// <summary>Gets the legacy DailyProfitTarget value; null indicates that its required source has not been captured.</summary>
    [Key(19)] public decimal? DailyProfitTarget { get; init; }
    /// <summary>Gets the legacy AssetPrice value; null indicates that its required source has not been captured.</summary>
    [Key(20)] public decimal? AssetPrice { get; init; }
    // Key 27 reserved for the retired textual VixVolatility classification.
    /// <summary>Gets the legacy PutOTMProbability value; null indicates that its required source has not been captured.</summary>
    [Key(31)] public double? PutOTMProbability { get; init; }
    /// <summary>Gets the legacy CallOTMProbability value; null indicates that its required source has not been captured.</summary>
    [Key(32)] public double? CallOTMProbability { get; init; }
    /// <summary>Gets the legacy ShortPutGamma value; null indicates that its required source has not been captured.</summary>
    [Key(33)] public double? ShortPutGamma { get; init; }
    /// <summary>Gets the legacy ShortCallGamma value; null indicates that its required source has not been captured.</summary>
    [Key(34)] public double? ShortCallGamma { get; init; }
    /// <summary>Gets the legacy NetPrice value; null indicates that its required source has not been captured.</summary>
    [Key(36)] public decimal? NetPrice { get; init; }
    /// <summary>Gets the legacy ForwardPrice value; null indicates that its required source has not been captured.</summary>
    [Key(37)] public decimal? ForwardPrice { get; init; }
    /// <summary>Gets the legacy ForwardDelta value; null indicates that its required source has not been captured.</summary>
    [Key(38)] public double? ForwardDelta { get; init; }
    /// <summary>Gets the legacy StopLossLimit value; null indicates that its required source has not been captured.</summary>
    [Key(39)] public double? StopLossLimit { get; init; }
    /// <summary>Gets the exact source position and monitoring identity.</summary>
    [Key(48)] public StrategyPositionSnapshot Position { get; init; } = new();
    /// <summary>Gets the source position event identity.</summary>
    [Key(49)] public Guid SourceEventId { get; init; }
    /// <summary>Gets explicit reasons preventing complete legacy monitoring.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public trade model contract.")]
    [Key(51)] public string[] UnavailableReasons { get; init; } = [];
    /// <summary>Gets the version of the recovered legacy formulas.</summary>
    [Key(52)] public string CalculationVersion { get; init; } = "Legacy/281550666/CurrentPnlLatestStop/v2";
    /// <summary>Gets whether all required calculation inputs were captured.</summary>
    [IgnoreMember] public bool IsComplete => CalculationStatus is not null ? string.Equals(CalculationStatus,"Complete",StringComparison.Ordinal) : UnavailableReasons.Length == 0
        && TradeType is not null
        && ForwardPrice.HasValue && ForwardLossRatio.HasValue
        && AssetPrice.HasValue && ShortPutGamma.HasValue && ShortCallGamma.HasValue
        && MaxProfit.HasValue && MaxLoss.HasValue;
}
