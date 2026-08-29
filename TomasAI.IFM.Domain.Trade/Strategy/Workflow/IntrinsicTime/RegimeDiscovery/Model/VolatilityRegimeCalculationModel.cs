using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Calculates the deterministic Volatility regime from one frozen snapshot.</summary>
public sealed class VolatilityRegimeCalculationModel
{
    /// <summary>Calculates one complete Volatility specialist result.</summary>
    /// <param name="input">Immutable calculation input.</param>
    /// <returns>The deterministic Volatility result or an incomplete result with failure reasons.</returns>
    public VolatilityRegimeResult Calculate(RegimeDiscoveryCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var config = input.ParameterSet.Volatility;
        var horizon = input.ParameterSet.TargetHorizon;
        var vixSpot = Find(input, RegimeDiscoverySignalMetric.VixLevel,
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily);
        var vxFront = Find(input, RegimeDiscoverySignalMetric.VxFrontLevel, horizon);
        var levelInput = RegimeDiscoveryMath.IsAvailable(vixSpot) ? vixSpot : vxFront;
        var atrRatio = Find(input, RegimeDiscoverySignalMetric.AtrBaselineRatio, horizon);
        var vxRatio = Find(input, RegimeDiscoverySignalMetric.VxFrontSecondRatio,
            TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily);
        var realized = Find(input, RegimeDiscoverySignalMetric.RealizedVolatilityPercentile, horizon);
        var priorComposite = Find(input, RegimeDiscoverySignalMetric.PriorVolatilityComposite, horizon);
        var required = new[] { levelInput, atrRatio, vxRatio };
        if (required.Any(observation => !RegimeDiscoveryMath.IsAvailable(observation)))
        {
            var failedReasons = required.Where(observation => !RegimeDiscoveryMath.IsAvailable(observation))
                .Select(observation => RegimeDiscoveryMath.MissingReason(
                    RegimeEvidenceArea.Volatility, observation, true))
                .Append(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.SpecialistFailed,
                    RegimeReasonSeverity.Failure, RegimeEvidenceArea.Volatility));
            return new VolatilityRegimeResult
            {
                IsComplete = false,
                Reasons = RegimeDiscoveryMath.OrderReasons(failedReasons)
            };
        }

        var vixScore = VixScore(levelInput!.Value, config.VixNormalBoundary, config.VixHighBoundary,
            config.VixExtremeBoundary, config.VixMaximum);
        var atrScore = RegimeDiscoveryMath.Piecewise(atrRatio!.Value,
            (0.75m, 0m), (1m, 0.40m), (1.50m, 0.75m), (2m, 1m));
        var vxScore = RegimeDiscoveryMath.Piecewise(vxRatio!.Value,
            (0.95m, 0.10m), (1m, 0.30m), (1.05m, 0.60m), (1.10m, 0.90m));
        var realizedAvailable = RegimeDiscoveryMath.IsAvailable(realized);
        var values = new[]
        {
            new WeightedValue(vixScore, config.VixWeight, levelInput.FreshnessFactor),
            new WeightedValue(atrScore, config.AtrRatioWeight, atrRatio.FreshnessFactor),
            new WeightedValue(vxScore, config.TermStructureWeight, vxRatio.FreshnessFactor),
            new WeightedValue(realizedAvailable ? RegimeDiscoveryMath.Clamp(realized!.Value) : 0m,
                config.RealizedVolatilityWeight, realized?.FreshnessFactor ?? 0m, realizedAvailable)
        };
        var score = RegimeDiscoveryMath.WeightedScore(values);
        var confidence = RegimeDiscoveryMath.Confidence(values);
        var level = score switch
        {
            < 0.25m => VolatilityRegimeLevel.Low,
            < 0.50m => VolatilityRegimeLevel.Normal,
            < 0.75m => VolatilityRegimeLevel.High,
            _ => VolatilityRegimeLevel.Extreme
        };
        var change = !RegimeDiscoveryMath.IsAvailable(priorComposite)
            ? VolatilityRegimeChange.Stable
            : score - priorComposite!.Value >= config.ExpansionThreshold
                ? VolatilityRegimeChange.Expanding
                : priorComposite.Value - score >= config.ExpansionThreshold
                    ? VolatilityRegimeChange.Contracting
                    : VolatilityRegimeChange.Stable;
        var termStructure = vxRatio.Value < 1m
            ? VxTermStructureRegime.Contango
            : vxRatio.Value > 1m ? VxTermStructureRegime.Backwardation : VxTermStructureRegime.Flat;
        var noNewTrade = levelInput.Value >= config.VixExtremeBoundary || score >= 0.75m ||
                         vxRatio.Value >= config.SevereBackwardationRatio;
        var evidence = new[]
        {
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Volatility,
                RegimeDiscoveryMath.IsAvailable(vixSpot) ? "VIX_SPOT" : "VX_FRONT_LEVEL",
                levelInput, vixScore,
                config.VixWeight, true),
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Volatility, "ATR_RATIO", atrRatio, atrScore,
                config.AtrRatioWeight, true),
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Volatility, "VX_TERM_STRUCTURE", vxRatio, vxScore,
                config.TermStructureWeight, true),
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Volatility, "REALIZED_VOLATILITY", realized,
                realizedAvailable ? realized!.Value : 0m, config.RealizedVolatilityWeight, false)
        };
        var reasons = new List<RegimeDiscoveryReason>
        {
            RegimeDiscoveryMath.Reason(level switch
            {
                VolatilityRegimeLevel.Low => RegimeDiscoveryReasonCodes.VolatilityLow,
                VolatilityRegimeLevel.Normal => RegimeDiscoveryReasonCodes.VolatilityNormal,
                VolatilityRegimeLevel.High => RegimeDiscoveryReasonCodes.VolatilityHigh,
                _ => RegimeDiscoveryReasonCodes.VolatilityExtreme
            }, level == VolatilityRegimeLevel.Extreme ? RegimeReasonSeverity.Restriction :
                RegimeReasonSeverity.Information, RegimeEvidenceArea.Volatility),
            RegimeDiscoveryMath.Reason(termStructure == VxTermStructureRegime.Backwardation
                ? RegimeDiscoveryReasonCodes.VolatilityBackwardation
                : RegimeDiscoveryReasonCodes.VolatilityContango,
                termStructure == VxTermStructureRegime.Backwardation ? RegimeReasonSeverity.Warning :
                    RegimeReasonSeverity.Information, RegimeEvidenceArea.Volatility)
        };
        if (levelInput.Value >= config.VixExtremeBoundary && level != VolatilityRegimeLevel.Extreme)
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.VolatilityExtreme,
                RegimeReasonSeverity.Restriction, RegimeEvidenceArea.Volatility, levelInput));
        if (change == VolatilityRegimeChange.Expanding)
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.VolatilityExpanding,
                RegimeReasonSeverity.Warning, RegimeEvidenceArea.Volatility));
        if (change == VolatilityRegimeChange.Contracting)
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.VolatilityContracting,
                RegimeReasonSeverity.Information, RegimeEvidenceArea.Volatility));
        if (!realizedAvailable)
            reasons.Add(RegimeDiscoveryMath.MissingReason(RegimeEvidenceArea.Volatility, realized, false));

        return new VolatilityRegimeResult
        {
            IsComplete = true,
            Level = level,
            Change = change,
            TermStructure = termStructure,
            Score = score,
            Confidence = confidence.Confidence,
            ConfidenceBand = RegimeDiscoveryMath.ConfidenceBand(confidence.Confidence),
            NoNewTrade = noNewTrade,
            Evidence = RegimeDiscoveryMath.OrderEvidence(evidence),
            Reasons = RegimeDiscoveryMath.OrderReasons(reasons)
        };
    }

    static RegimeDiscoverySignalObservation? Find(
        RegimeDiscoveryCalculationInput input,
        RegimeDiscoverySignalMetric metric,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType horizon) =>
        RegimeDiscoveryMath.FindAny(input, metric, horizon);

    static decimal VixScore(decimal value, decimal normal, decimal high, decimal extreme, decimal maximum)
    {
        if (value < normal) return RegimeDiscoveryMath.Clamp(value / normal * 0.25m);
        return RegimeDiscoveryMath.Piecewise(value,
            (normal, 0.25m), (high, 0.50m), (extreme, 0.75m), (maximum, 1m));
    }
}
