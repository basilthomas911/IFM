using TomasAI.IFM.Domain.MarketData.Analytics.Shared.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;

/// <summary>Calculates the deterministic directional Trend regime from one frozen snapshot.</summary>
public sealed class TrendRegimeCalculationModel
{
    /// <summary>Calculates one complete Trend specialist result.</summary>
    /// <param name="input">Immutable calculation input.</param>
    /// <returns>The deterministic Trend result or an incomplete result with failure reasons.</returns>
    public TrendRegimeResult Calculate(RegimeDiscoveryCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var configuration = input.ParameterSet.Trend;
        var evidence = new List<RegimeDiscoveryEvidence>();
        var reasons = new List<RegimeDiscoveryReason>();
        var frameValues = new List<WeightedValue>();
        var momentum = new List<decimal>();

        foreach (var frame in input.ParameterSet.Horizon.TimeFrames)
        {
            var result = CalculateFrame(input, frame, configuration, evidence, reasons);
            if (result.IsComplete)
            {
                frameValues.Add(new(result.Score, frame.Weight, result.Freshness));
                if (frame.IsRequired) momentum.AddRange(result.Momentum);
            }
            else if (!frame.IsRequired)
            {
                frameValues.Add(new(0m, frame.Weight, 0m, false));
            }
        }

        var requiredFailure = reasons.Any(reason => reason.Severity == RegimeReasonSeverity.Failure);
        if (requiredFailure || frameValues.All(value => !value.IsAvailable))
            return Incomplete(evidence, reasons);

        var score = RegimeDiscoveryMath.WeightedScore(frameValues);
        var confidence = RegimeDiscoveryMath.Confidence(frameValues);
        var direction = Direction(score, configuration.DirectionThreshold);
        var strength = Strength(score, configuration);
        var itiDirection = Observation(input, RegimeDiscoverySignalMetric.ItiDirection)?.Value ?? 0m;
        var bandLevel = Observation(input, RegimeDiscoverySignalMetric.ItiBandLevel)?.Value ?? 0m;
        var reversalLevel = Observation(input, RegimeDiscoverySignalMetric.ItiReversalLevel)?.Value ?? 0m;
        var phase = Phase(direction, bandLevel, reversalLevel, momentum, itiDirection, input);

        reasons.Add(RegimeDiscoveryMath.Reason(
            direction switch
            {
                RegimeDirection.Up => RegimeDiscoveryReasonCodes.TrendUp,
                RegimeDirection.Down => RegimeDiscoveryReasonCodes.TrendDown,
                _ => RegimeDiscoveryReasonCodes.TrendNeutral
            }, RegimeReasonSeverity.Information, RegimeEvidenceArea.Trend));
        if (phase == TrendRegimePhase.Reversing)
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.TrendReversing,
                RegimeReasonSeverity.Warning, RegimeEvidenceArea.Trend));
        if (confidence.Agreement < 0.60m)
            reasons.Add(RegimeDiscoveryMath.Reason(RegimeDiscoveryReasonCodes.TrendTimeFrameConflict,
                RegimeReasonSeverity.Warning, RegimeEvidenceArea.Trend));

        return new TrendRegimeResult
        {
            IsComplete = true,
            Direction = direction,
            Strength = strength,
            Phase = phase,
            Score = score,
            Confidence = confidence.Confidence,
            ConfidenceBand = RegimeDiscoveryMath.ConfidenceBand(confidence.Confidence),
            TimeFrameAgreement = confidence.Agreement,
            Evidence = RegimeDiscoveryMath.OrderEvidence(evidence),
            Reasons = RegimeDiscoveryMath.OrderReasons(reasons)
        };
    }

    static FrameResult CalculateFrame(
        RegimeDiscoveryCalculationInput input,
        RegimeDiscoveryTimeFrameConfiguration frame,
        TrendRegimeConfiguration configuration,
        ICollection<RegimeDiscoveryEvidence> evidence,
        ICollection<RegimeDiscoveryReason> reasons)
    {
        var requiredMetrics = new[]
        {
            RegimeDiscoverySignalMetric.CurrentPrice,
            RegimeDiscoverySignalMetric.Ema20,
            RegimeDiscoverySignalMetric.Ema50,
            RegimeDiscoverySignalMetric.Ema200,
            RegimeDiscoverySignalMetric.Ema20Slope,
            RegimeDiscoverySignalMetric.Ema50Slope,
            RegimeDiscoverySignalMetric.Ema200Slope,
            RegimeDiscoverySignalMetric.Rsi14,
            RegimeDiscoverySignalMetric.Rsi14Slope,
            RegimeDiscoverySignalMetric.Adx14,
            RegimeDiscoverySignalMetric.PlusDi14,
            RegimeDiscoverySignalMetric.MinusDi14,
            RegimeDiscoverySignalMetric.MacdHistogram,
            RegimeDiscoverySignalMetric.Atr14
        };
        var observations = requiredMetrics.ToDictionary(
            metric => metric,
            metric => RegimeDiscoveryMath.Find(input, metric, frame.TimeFrame));
        if (observations.Values.Any(observation => !RegimeDiscoveryMath.IsAvailable(observation)))
        {
            foreach (var observation in observations.Values.Where(value => !RegimeDiscoveryMath.IsAvailable(value)))
                reasons.Add(RegimeDiscoveryMath.MissingReason(RegimeEvidenceArea.Trend, observation, frame.IsRequired));
            return default;
        }

        var price = observations[RegimeDiscoverySignalMetric.CurrentPrice]!.Value;
        var ema20 = observations[RegimeDiscoverySignalMetric.Ema20]!.Value;
        var ema50 = observations[RegimeDiscoverySignalMetric.Ema50]!.Value;
        var ema200 = observations[RegimeDiscoverySignalMetric.Ema200]!.Value;
        var alignment = RegimeDiscoveryMath.Round((RegimeDiscoveryMath.Sign(price - ema20) +
            RegimeDiscoveryMath.Sign(ema20 - ema50) + RegimeDiscoveryMath.Sign(ema50 - ema200)) / 3m);
        var slopes = RegimeDiscoveryMath.Round((
            RegimeDiscoveryMath.SignedClamp(observations[RegimeDiscoverySignalMetric.Ema20Slope]!.Value /
                                            configuration.EmaSlopeScale) +
            RegimeDiscoveryMath.SignedClamp(observations[RegimeDiscoverySignalMetric.Ema50Slope]!.Value /
                                            configuration.EmaSlopeScale) +
            RegimeDiscoveryMath.SignedClamp(observations[RegimeDiscoverySignalMetric.Ema200Slope]!.Value /
                                            configuration.EmaSlopeScale)) / 3m);
        var rsi = observations[RegimeDiscoverySignalMetric.Rsi14]!.Value;
        var rsiSlope = observations[RegimeDiscoverySignalMetric.Rsi14Slope]!.Value;
        var rsiScore = RegimeDiscoveryMath.Round(0.70m * RegimeDiscoveryMath.SignedClamp((rsi - 50m) / 20m) +
                                                 0.30m * RegimeDiscoveryMath.SignedClamp(
                                                     rsiSlope / configuration.RsiSlopeScale));
        var adx = observations[RegimeDiscoverySignalMetric.Adx14]!.Value;
        var plusDi = observations[RegimeDiscoverySignalMetric.PlusDi14]!.Value;
        var minusDi = observations[RegimeDiscoverySignalMetric.MinusDi14]!.Value;
        var adxScore = RegimeDiscoveryMath.Round(RegimeDiscoveryMath.Sign(plusDi - minusDi) *
                                                 RegimeDiscoveryMath.Clamp((adx - 15m) / 25m));
        var atr = observations[RegimeDiscoverySignalMetric.Atr14]!.Value;
        var macd = observations[RegimeDiscoverySignalMetric.MacdHistogram]!.Value;
        var macdScore = atr <= 0m ? 0m : RegimeDiscoveryMath.SignedClamp(macd / atr / configuration.MacdAtrScale);
        var itiDirection = Observation(input, RegimeDiscoverySignalMetric.ItiDirection);
        var itiBand = Observation(input, RegimeDiscoverySignalMetric.ItiBandLevel);
        var itiReversal = Observation(input, RegimeDiscoverySignalMetric.ItiReversalLevel);
        var tdi = RegimeDiscoveryMath.Find(input, RegimeDiscoverySignalMetric.Tdi, frame.TimeFrame);
        if (!RegimeDiscoveryMath.IsAvailable(itiDirection) || !RegimeDiscoveryMath.IsAvailable(itiBand) ||
            !RegimeDiscoveryMath.IsAvailable(itiReversal))
        {
            reasons.Add(RegimeDiscoveryMath.MissingReason(RegimeEvidenceArea.Trend, itiDirection, true));
            return default;
        }
        var itiScore = RegimeDiscoveryMath.Round(RegimeDiscoveryMath.Sign(itiDirection!.Value) *
            RegimeDiscoveryMath.Clamp(itiBand!.Value) * (1m - RegimeDiscoveryMath.Clamp(itiReversal!.Value)));
        var tdiWeight = !RegimeDiscoveryMath.IsAvailable(tdi)
            ? 0m : configuration.ItiWeight * 0.25m;
        var itiWeight = configuration.ItiWeight - tdiWeight;
        var components = new[]
        {
            new WeightedValue(alignment, configuration.EmaAlignmentWeight, MinimumFreshness(observations.Values)),
            new WeightedValue(slopes, configuration.EmaSlopeWeight, MinimumFreshness(observations.Values)),
            new WeightedValue(rsiScore, configuration.RsiWeight, MinimumFreshness(observations.Values)),
            new WeightedValue(adxScore, configuration.AdxWeight, MinimumFreshness(observations.Values)),
            new WeightedValue(macdScore, configuration.MacdWeight, MinimumFreshness(observations.Values)),
            new WeightedValue(itiScore, itiWeight, itiDirection.FreshnessFactor),
            new WeightedValue(tdi?.Value ?? 0m, tdiWeight, tdi?.FreshnessFactor ?? 0m,
                RegimeDiscoveryMath.IsAvailable(tdi))
        };
        evidence.Add(RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Trend, "EMA_ALIGNMENT",
            observations[RegimeDiscoverySignalMetric.Ema20], alignment, configuration.EmaAlignmentWeight, frame.IsRequired));
        evidence.Add(RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Trend, "EMA_SLOPES",
            observations[RegimeDiscoverySignalMetric.Ema20Slope], slopes, configuration.EmaSlopeWeight, frame.IsRequired));
        evidence.Add(RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Trend, "RSI",
            observations[RegimeDiscoverySignalMetric.Rsi14], rsiScore, configuration.RsiWeight, frame.IsRequired));
        evidence.Add(RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Trend, "ADX",
            observations[RegimeDiscoverySignalMetric.Adx14], adxScore, configuration.AdxWeight, frame.IsRequired));
        evidence.Add(RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Trend, "MACD",
            observations[RegimeDiscoverySignalMetric.MacdHistogram], macdScore, configuration.MacdWeight, frame.IsRequired));
        evidence.Add(RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Trend, "ITI",
            itiDirection, itiScore, itiWeight, true));
        evidence.Add(RegimeDiscoveryMath.Evidence(RegimeEvidenceArea.Trend, "TDI_CONFIRMATION",
            tdi, tdi?.Value ?? 0m, configuration.ItiWeight * 0.25m, false));
        if (!RegimeDiscoveryMath.IsAvailable(tdi))
        {
            reasons.Add(RegimeDiscoveryMath.MissingReason(RegimeEvidenceArea.Trend, tdi, false));
        }
        return new(true, RegimeDiscoveryMath.WeightedScore(components),
            components.Where(component => component.Weight > 0m && component.IsAvailable)
                .Min(component => component.FreshnessFactor), [rsiScore, adxScore, macdScore]);
    }

    static RegimeDiscoverySignalObservation? Observation(
        RegimeDiscoveryCalculationInput input, RegimeDiscoverySignalMetric metric) =>
        RegimeDiscoveryMath.FindAny(input, metric, input.ParameterSet.TargetHorizon);

    static decimal MinimumFreshness(IEnumerable<RegimeDiscoverySignalObservation?> observations) =>
        observations.Where(observation => observation is not null).Min(observation => observation!.FreshnessFactor);

    static RegimeDirection Direction(decimal score, decimal threshold) =>
        score >= threshold ? RegimeDirection.Up : score <= -threshold ? RegimeDirection.Down : RegimeDirection.Neutral;

    static TrendRegimeStrength Strength(decimal score, TrendRegimeConfiguration configuration)
    {
        var absolute = Math.Abs(score);
        if (absolute < configuration.DirectionThreshold) return TrendRegimeStrength.None;
        if (absolute < configuration.ModerateThreshold) return TrendRegimeStrength.Weak;
        if (absolute < configuration.StrongThreshold) return TrendRegimeStrength.Moderate;
        if (absolute < configuration.ExtremeThreshold) return TrendRegimeStrength.Strong;
        return TrendRegimeStrength.Extreme;
    }

    static TrendRegimePhase Phase(
        RegimeDirection direction,
        decimal bandLevel,
        decimal reversalLevel,
        IEnumerable<decimal> momentum,
        decimal itiDirection,
        RegimeDiscoveryCalculationInput input)
    {
        if (direction == RegimeDirection.Neutral) return TrendRegimePhase.RangeBound;
        var opposing = momentum.Count(value => RegimeDiscoveryMath.Sign(value) != 0m &&
                                               RegimeDiscoveryMath.Sign(value) != RegimeDiscoveryMath.Sign(itiDirection));
        if (reversalLevel >= input.ParameterSet.Trend.ReversingThreshold && opposing >= 2)
            return TrendRegimePhase.Reversing;
        if (reversalLevel >= input.ParameterSet.Trend.ExhaustingReversalThreshold || opposing >= 2)
            return TrendRegimePhase.Exhausting;
        var adx = Observation(input, RegimeDiscoverySignalMetric.Adx14)?.Value ?? 0m;
        if (bandLevel < 1m || adx < 20m) return TrendRegimePhase.Emerging;
        return TrendRegimePhase.Established;
    }

    static TrendRegimeResult Incomplete(
        IEnumerable<RegimeDiscoveryEvidence> evidence,
        IEnumerable<RegimeDiscoveryReason> reasons) => new()
        {
            IsComplete = false,
            Direction = RegimeDirection.Unknown,
            Phase = TrendRegimePhase.Unknown,
            Evidence = RegimeDiscoveryMath.OrderEvidence(evidence),
            Reasons = RegimeDiscoveryMath.OrderReasons(reasons.Append(RegimeDiscoveryMath.Reason(
                RegimeDiscoveryReasonCodes.SpecialistFailed, RegimeReasonSeverity.Failure,
                RegimeEvidenceArea.Trend)))
        };

    readonly record struct FrameResult(bool IsComplete, decimal Score, decimal Freshness, decimal[] Momentum);
}
