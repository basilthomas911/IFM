using MessagePack;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Risk;

/// <summary>Explicit daily-loss monitoring settings. Monetary parameters use the common broker option contract currency.</summary>
[MessagePackObject]
public sealed record IronCondorRiskParameters
{
    /// <summary>Gets the positive daily loss threshold in broker currency.</summary>
    [Key(0), JsonRequired] public decimal DailyLossLimit { get; init; } = 1000m;
    /// <summary>Gets the future valuation horizon in seconds; 300 means five minutes.</summary>
    [Key(1), JsonRequired] public int ScenarioHorizonSeconds { get; init; } = 300;
    /// <summary>Gets the absolute underlying move in points; evaluate both plus and minus this value.</summary>
    [Key(2), JsonRequired] public decimal AdverseFuturesMovePoints { get; init; } = 10m;
    /// <summary>Gets the additive IV shift in percentage points; zero leaves IV unchanged.</summary>
    [Key(3), JsonRequired] public double ScenarioVolatilityShiftPercentagePoints { get; init; }
    /// <summary>Gets the ratio at which a negative daily PnL produces an exit recommendation.</summary>
    [Key(4), JsonRequired] public double ExitForwardLossRatio { get; init; } = 1;
    /// <summary>Gets the earlier warning ratio; warnings do not execute an exit.</summary>
    [Key(5), JsonRequired] public double WarningForwardLossRatio { get; init; } = .8;
    /// <summary>Gets the maximum allowed age of source quotes in seconds.</summary>
    [Key(6), JsonRequired] public int MaximumQuoteAgeSeconds { get; init; } = 30;
    /// <summary>Gets the provisional additional commission per closing option contract, in broker currency.</summary>
    [Key(7), JsonRequired] public decimal ExitCommissionPerContract { get; init; } = 5m;
    /// <summary>Gets additional slippage ticks per leg beyond the captured closing-price basis; zero means no extra tick allowance.</summary>
    [Key(8), JsonRequired] public int AdditionalExitSlippageTicksPerLeg { get; init; } = 1;

    /// <summary>Rejects missing, nonfinite or inconsistent daily-loss configuration.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This parameterless model validation reports malformed persisted instance content, not a caller argument.")]
    public void Validate()
    {
        if (DailyLossLimit <= 0 || ScenarioHorizonSeconds <= 0 || AdverseFuturesMovePoints <= 0
            || !double.IsFinite(ScenarioVolatilityShiftPercentagePoints)
            || !double.IsFinite(ExitForwardLossRatio) || ExitForwardLossRatio <= 0
            || !double.IsFinite(WarningForwardLossRatio) || WarningForwardLossRatio <= 0
            || WarningForwardLossRatio >= ExitForwardLossRatio || MaximumQuoteAgeSeconds <= 0
            || ExitCommissionPerContract < 0 || AdditionalExitSlippageTicksPerLeg < 0)
            throw new ArgumentException("IronCondorRiskParameters.INVALID");
    }
}
