using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

/// <summary>
/// Aggregates ITI (Intrinsic Time Indicator) signal snapshots representing distinct change events
/// (trend direction change, trend extreme change, and trend reversal change) for a futures contract.
/// </summary>
/// <remarks>
/// MessagePack serializable. Each component signal is optional (nullable) and may be absent if that
/// specific event type did not occur in the evaluated interval. Follows the serialization pattern
/// used by other view models (e.g., FundOrderReadModel).
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FuturesItiSignalDataReadModel
{
    /// <summary>
    /// ITI signal snapshot captured when a trend direction change is detected.
    /// </summary>
    [Key(0)]
    public FuturesItiSignalV2ReadModel? TrendDirectionChange { get; init; }

    /// <summary>
    /// ITI signal snapshot captured when a new trend extreme (high/low pivot) is detected.
    /// </summary>
    [Key(1)]
    public FuturesItiSignalV2ReadModel? TrendExtremeChange { get; init; }

    /// <summary>
    /// ITI signal snapshot captured when a trend reversal condition is detected.
    /// </summary>
    [Key(2)]
    public FuturesItiSignalV2ReadModel? TrendReversalChange { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack and tooling scenarios.
    /// </summary>
    public FuturesItiSignalDataReadModel() { }

    /// <summary>
    /// Initializes an ITI signal data aggregate with optional change-event snapshots.
    /// </summary>
    /// <param name="trendDirectionChange">Trend direction change signal (optional).</param>
    /// <param name="trendExtremeChange">Trend extreme change signal (optional).</param>
    /// <param name="trendReversalChange">Trend reversal change signal (optional).</param>
    public FuturesItiSignalDataReadModel(
        FuturesItiSignalV2ReadModel? trendDirectionChange,
        FuturesItiSignalV2ReadModel? trendExtremeChange,
        FuturesItiSignalV2ReadModel? trendReversalChange)
    {
        TrendDirectionChange = trendDirectionChange;
        TrendExtremeChange = trendExtremeChange;
        TrendReversalChange = trendReversalChange;
    }

    /// <summary>
    /// Returns a compact JSON representation for diagnostics/logging.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

