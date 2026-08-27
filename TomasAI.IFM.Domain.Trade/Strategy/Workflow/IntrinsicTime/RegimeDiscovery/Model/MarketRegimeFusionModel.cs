using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Fuses complete Trend, Volatility, and Market Structure results deterministically.</summary>
public sealed class MarketRegimeFusionModel
{
    /// <summary>Fuses the three specialist results without recalculating their evidence.</summary>
    /// <param name="trend">Complete Trend result.</param>
    /// <param name="volatility">Complete Volatility result.</param>
    /// <param name="marketStructure">Complete Market Structure result.</param>
    /// <param name="configuration">Immutable Fusion configuration.</param>
    /// <returns>The complete fused result, or an incomplete result when a specialist is incomplete.</returns>
    public MarketRegimeFusionResult Calculate(
        TrendRegimeResult trend,
        VolatilityRegimeResult volatility,
        MarketStructureRegimeResult marketStructure,
        MarketRegimeFusionConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(trend);
        ArgumentNullException.ThrowIfNull(volatility);
        ArgumentNullException.ThrowIfNull(marketStructure);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!trend.IsComplete || !volatility.IsComplete || !marketStructure.IsComplete)
            return new MarketRegimeFusionResult
            {
                IsComplete = false,
                Reasons = RegimeDiscoveryMath.OrderReasons([
                    RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.FusionFailed,
                        RegimeReasonSeverity.Failure, RegimeEvidenceArea.Fusion)])
            };

        var directionalScore = RegimeDiscoveryMath.Round(
            configuration.TrendDirectionalWeight * trend.Score +
            configuration.MarketStructureDirectionalWeight * marketStructure.Score);
        var direction = directionalScore >= configuration.DirectionThreshold
            ? RegimeDirection.Up
            : directionalScore <= -configuration.DirectionThreshold
                ? RegimeDirection.Down
                : RegimeDirection.Neutral;
        var conviction = RegimeDiscoveryMath.Round(Math.Abs(directionalScore) *
            (1m - configuration.VolatilityConvictionPenalty * volatility.Score));
        var baseConfidence = RegimeDiscoveryMath.Round(
            configuration.TrendConfidenceWeight * trend.Confidence +
            configuration.VolatilityConfidenceWeight * volatility.Confidence +
            configuration.MarketStructureConfidenceWeight * marketStructure.Confidence);
        var alignment = RegimeDiscoveryMath.Clamp(1m - Math.Abs(trend.Score - marketStructure.Score) / 2m);
        var confidence = RegimeDiscoveryMath.Clamp(baseConfidence * (0.75m + 0.25m * alignment));
        var restrictions = new List<RegimeRestriction>();
        var reasons = new List<RegimeDiscoveryReason>();
        if (volatility.NoNewTrade || volatility.Level == VolatilityRegimeLevel.Extreme)
        {
            restrictions.Add(RegimeRestriction.NoNewTrade);
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.FusionNoNewTrade,
                RegimeReasonSeverity.Restriction, RegimeEvidenceArea.Fusion));
        }
        if (IsDirectional(trend.Direction) && IsDirectional(marketStructure.Direction) &&
            trend.Direction != marketStructure.Direction)
        {
            restrictions.Add(RegimeRestriction.DirectionConflict);
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.FusionDirectionConflict,
                RegimeReasonSeverity.Restriction, RegimeEvidenceArea.Fusion));
        }
        else
        {
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.FusionAligned,
                RegimeReasonSeverity.Information, RegimeEvidenceArea.Fusion));
        }
        if (confidence < configuration.LowConfidenceRestrictionThreshold)
        {
            restrictions.Add(RegimeRestriction.LowConfidence);
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.FusionLowConfidence,
                RegimeReasonSeverity.Restriction, RegimeEvidenceArea.Fusion));
        }
        if (marketStructure.Classification == MarketStructureClassification.Transitioning)
        {
            restrictions.Add(RegimeRestriction.Transition);
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.FusionTransition,
                RegimeReasonSeverity.Restriction, RegimeEvidenceArea.Fusion));
        }
        var optionalMissing = trend.Reasons.Concat(volatility.Reasons).Concat(marketStructure.Reasons)
            .Any(reason => reason.Code == RegimeDiscoveryReasonCodes.OptionalDataMissing);
        var quality = confidence >= configuration.HighQualityThreshold && !optionalMissing && restrictions.Count == 0
            ? RegimeOverallQuality.High
            : confidence >= configuration.AcceptableQualityThreshold && !optionalMissing && restrictions.Count == 0
                ? RegimeOverallQuality.Acceptable
                : confidence < 0.35m ? RegimeOverallQuality.Low : RegimeOverallQuality.Degraded;

        return new MarketRegimeFusionResult
        {
            IsComplete = true,
            Direction = direction,
            DirectionalScore = directionalScore,
            RiskAdjustedConviction = conviction,
            Confidence = confidence,
            ConfidenceBand = RegimeDiscoveryMath.ConfidenceBand(confidence),
            Quality = quality,
            Restrictions = restrictions.Distinct().Order().ToArray(),
            Reasons = RegimeDiscoveryMath.OrderReasons(reasons)
        };
    }

    static bool IsDirectional(RegimeDirection direction) =>
        direction is RegimeDirection.Up or RegimeDirection.Down;
}
