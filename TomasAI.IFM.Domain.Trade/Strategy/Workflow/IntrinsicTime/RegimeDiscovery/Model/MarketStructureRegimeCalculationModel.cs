using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Calculates deterministic Market Structure from one frozen snapshot.</summary>
public sealed class MarketStructureRegimeCalculationModel
{
    /// <summary>Calculates one complete Market Structure specialist result.</summary>
    /// <param name="input">Immutable calculation input.</param>
    /// <returns>The deterministic structure result or an incomplete result with failure reasons.</returns>
    public MarketStructureRegimeResult Calculate(RegimeDiscoveryCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var config = input.ParameterSet.MarketStructure;
        var horizon = input.ParameterSet.TargetHorizon;
        var price = Find(input, RegimeDiscoverySignalMetric.CurrentPrice, horizon);
        var widthRatio = Find(input, RegimeDiscoverySignalMetric.BollingerWidthRatio, horizon);
        var position = Find(input, RegimeDiscoverySignalMetric.BollingerPosition, horizon);
        var emaInteraction = Find(input, RegimeDiscoverySignalMetric.Ema20Interaction, horizon);
        var range = Find(input, RegimeDiscoverySignalMetric.AtrNormalizedRange, horizon);
        var atrRatio = Find(input, RegimeDiscoverySignalMetric.AtrBaselineRatio, horizon);
        var rollingHigh = Find(input, RegimeDiscoverySignalMetric.RollingHigh20, horizon);
        var rollingLow = Find(input, RegimeDiscoverySignalMetric.RollingLow20, horizon);
        var breakoutDistance = Find(input, RegimeDiscoverySignalMetric.BreakoutDistanceAtr, horizon);
        var itiDirection = Find(input, RegimeDiscoverySignalMetric.ItiDirection, horizon);
        var itiBand = Find(input, RegimeDiscoverySignalMetric.ItiBandLevel, horizon);
        var itiReversal = Find(input, RegimeDiscoverySignalMetric.ItiReversalLevel, horizon);
        var required = new[]
        {
            price, widthRatio, position, emaInteraction, range, atrRatio, rollingHigh, rollingLow,
            breakoutDistance, itiDirection, itiBand, itiReversal
        };
        if (required.Any(observation => !RegimeDiscoveryMath.IsAvailable(observation)))
        {
            var failedReasons = required.Where(observation => !RegimeDiscoveryMath.IsAvailable(observation))
                .Select(observation => RegimeDiscoveryMath.MissingReason(
                    RegimeEvidenceArea.MarketStructure, observation, true))
                .Append(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.SpecialistFailed,
                    RegimeReasonSeverity.Failure, RegimeEvidenceArea.MarketStructure));
            return new MarketStructureRegimeResult
            {
                IsComplete = false,
                Classification = MarketStructureClassification.Unknown,
                Direction = RegimeDirection.Unknown,
                Reasons = RegimeDiscoveryMath.OrderReasons(failedReasons)
            };
        }

        var persistence = RegimeDiscoveryMath.Clamp(itiBand!.Value) *
                          (1m - RegimeDiscoveryMath.Clamp(itiReversal!.Value));
        var organization = RegimeDiscoveryMath.Round((
            RegimeDiscoveryMath.Sign(emaInteraction!.Value) +
            RegimeDiscoveryMath.Sign(position!.Value) +
            RegimeDiscoveryMath.Sign(itiDirection!.Value) * persistence) / 3m);
        var breakout = breakoutDistance!.Value >= config.BreakoutAtrThreshold
            ? MarketBreakoutState.Up
            : breakoutDistance.Value <= -config.BreakoutAtrThreshold
                ? MarketBreakoutState.Down
                : MarketBreakoutState.None;
        var classification = breakout != MarketBreakoutState.None
            ? MarketStructureClassification.BreakingOut
            : widthRatio!.Value <= config.CompressionWidthRatio && atrRatio!.Value <= config.CompressionAtrRatio
                ? MarketStructureClassification.Compressing
                : widthRatio.Value >= config.ExpansionWidthRatio || atrRatio.Value >= config.ExpansionAtrRatio
                    ? MarketStructureClassification.Expanding
                    : Math.Abs(organization) >= config.TrendingOrganizationThreshold &&
                      persistence >= config.TrendingPersistenceThreshold
                        ? MarketStructureClassification.Trending
                        : Math.Abs(organization) < config.RangingOrganizationThreshold &&
                          widthRatio.Value >= config.CompressionWidthRatio &&
                          widthRatio.Value <= config.ExpansionWidthRatio
                            ? MarketStructureClassification.Ranging
                            : MarketStructureClassification.Transitioning;
        var score = classification switch
        {
            MarketStructureClassification.BreakingOut => RegimeDiscoveryMath.SignedClamp(
                breakoutDistance.Value / 2m),
            MarketStructureClassification.Trending => organization,
            MarketStructureClassification.Expanding => RegimeDiscoveryMath.Sign(organization),
            _ => 0m
        };
        var direction = score > 0m ? RegimeDirection.Up : score < 0m ? RegimeDirection.Down : RegimeDirection.Neutral;
        var values = new[]
        {
            new WeightedValue(RegimeDiscoveryMath.SignedClamp(position.Value), config.BollingerWeight,
                widthRatio.FreshnessFactor),
            new WeightedValue(RegimeDiscoveryMath.SignedClamp(emaInteraction.Value),
                config.EmaInteractionWeight, emaInteraction.FreshnessFactor),
            new WeightedValue(RegimeDiscoveryMath.SignedClamp(range!.Value * RegimeDiscoveryMath.Sign(organization)),
                config.AtrRangeWeight, range.FreshnessFactor),
            new WeightedValue(RegimeDiscoveryMath.SignedClamp(breakoutDistance.Value), config.BreakoutWeight,
                breakoutDistance.FreshnessFactor),
            new WeightedValue(RegimeDiscoveryMath.Sign(itiDirection.Value) * persistence, config.ItiWeight,
                itiDirection.FreshnessFactor)
        };
        var confidence = RegimeDiscoveryMath.Confidence(values);
        var evidence = new[]
        {
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.MarketStructure, "BOLLINGER", widthRatio,
                RegimeDiscoveryMath.SignedClamp(position.Value), config.BollingerWeight, true),
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.MarketStructure, "EMA20_INTERACTION", emaInteraction,
                RegimeDiscoveryMath.SignedClamp(emaInteraction.Value), config.EmaInteractionWeight, true),
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.MarketStructure, "ATR_RANGE", range,
                RegimeDiscoveryMath.SignedClamp(range.Value * RegimeDiscoveryMath.Sign(organization)),
                config.AtrRangeWeight, true),
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.MarketStructure, "BREAKOUT", breakoutDistance,
                RegimeDiscoveryMath.SignedClamp(breakoutDistance.Value), config.BreakoutWeight, true),
            RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.MarketStructure, "ITI_PERSISTENCE", itiDirection,
                RegimeDiscoveryMath.Sign(itiDirection.Value) * persistence, config.ItiWeight, true)
        };
        var reasonCode = classification switch
        {
            MarketStructureClassification.BreakingOut when breakout == MarketBreakoutState.Up =>
                RegimeDiscoveryReasonCodes.StructureBreakoutUp,
            MarketStructureClassification.BreakingOut => RegimeDiscoveryReasonCodes.StructureBreakoutDown,
            MarketStructureClassification.Trending => RegimeDiscoveryReasonCodes.StructureTrending,
            MarketStructureClassification.Ranging => RegimeDiscoveryReasonCodes.StructureRanging,
            MarketStructureClassification.Compressing => RegimeDiscoveryReasonCodes.StructureCompressing,
            MarketStructureClassification.Expanding => RegimeDiscoveryReasonCodes.StructureExpanding,
            _ => RegimeDiscoveryReasonCodes.StructureTransitioning
        };

        return new MarketStructureRegimeResult
        {
            IsComplete = true,
            Classification = classification,
            Direction = direction,
            Breakout = breakout,
            Score = RegimeDiscoveryMath.Round(score),
            Confidence = confidence.Confidence,
            ConfidenceBand = RegimeDiscoveryMath.ConfidenceBand(confidence.Confidence),
            Evidence = RegimeDiscoveryMath.OrderEvidence(evidence),
            Reasons = RegimeDiscoveryMath.OrderReasons([
                RegimeDiscoveryMath.Reason(reasonCode, classification == MarketStructureClassification.Transitioning
                    ? RegimeReasonSeverity.Warning : RegimeReasonSeverity.Information,
                    RegimeEvidenceArea.MarketStructure)])
        };
    }

    static RegimeDiscoverySignalObservation? Find(
        RegimeDiscoveryCalculationInput input,
        RegimeDiscoverySignalMetric metric,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType horizon) =>
        RegimeDiscoveryMath.FindAny(input.Snapshot, metric, horizon);
}
